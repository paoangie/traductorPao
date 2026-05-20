using System.Security.Claims;
using System.Text.Json;
using Api_TutorIdiomas.Models;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExercisesController : ControllerBase
    {
        private const int DYNAMIC_ID_OFFSET = 1000000;

        private readonly IExerciseRepository _exerciseRepo;
        private readonly IProgressRepository _progressRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly IMistakeRepository _mistakeRepo;
        private readonly ExerciseScoringService _scoringService;
        private readonly DynamicExerciseService _dynamicExerciseService;
        private readonly ILogger<ExercisesController> _logger;

        public ExercisesController(
            IExerciseRepository exerciseRepo,
            IProgressRepository progressRepo,
            ILessonRepository lessonRepo,
            IMistakeRepository mistakeRepo,
            ExerciseScoringService scoringService,
            DynamicExerciseService dynamicExerciseService,
            ILogger<ExercisesController> logger)
        {
            _exerciseRepo = exerciseRepo;
            _progressRepo = progressRepo;
            _lessonRepo = lessonRepo;
            _mistakeRepo = mistakeRepo;
            _scoringService = scoringService;
            _dynamicExerciseService = dynamicExerciseService;
            _logger = logger;
        }

        [HttpGet("lesson/{lessonId}")]
        public async Task<IActionResult> GetByLesson(int lessonId)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Unauthorized(new { error = "Usuario no autenticado" });
                var userId = Guid.Parse(userIdStr);

                var dynamicExercises = await _dynamicExerciseService.GenerateExercisesAsync(lessonId, userId, 3);

                var result = dynamicExercises.Select((ex, i) => new
                {
                    id = DYNAMIC_ID_OFFSET + lessonId * 100 + i,
                    lessonId,
                    type = ex.Type,
                    content = (object)BuildContent(ex)
                }).ToList();

                return Ok(result);
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar ejercicios para la lección {LessonId}", lessonId);
                throw;
            }
        }

        private static object BuildContent(DynamicExerciseDto ex)
        {
            var content = new Dictionary<string, object?>
            {
                ["hint"] = ex.Hint
            };

            switch (ex.Type)
            {
                case "translation":
                    content["question"] = ex.Question;
                    content["answer"] = ex.Answer;
                    break;
                case "grammar":
                    content["question"] = ex.Question;
                    content["correct"] = ex.Correct;
                    content["options"] = ex.Options ?? new List<string>();
                    break;
                case "pronunciation":
                    content["phrase"] = ex.Phrase;
                    break;
            }

            return content;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                if (id >= DYNAMIC_ID_OFFSET)
                    return NotFound(new { error = "Ejercicio dinámico no disponible por ID" });

                var exercise = await _exerciseRepo.GetByIdAsync(id);
                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });
                return Ok(exercise);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ejercicio {Id}", id);
                throw;
            }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitAnswer(int id, [FromBody] ExerciseSubmitDto request)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var userId = Guid.Parse(userIdStr);

                if (id >= DYNAMIC_ID_OFFSET || !string.IsNullOrEmpty(request.ExerciseType))
                {
                    return Ok(await EvaluateDynamicExercise(request, userId));
                }

                var exercise = await _exerciseRepo.GetByIdAsync(id);
                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });

                var result = exercise.Type switch
                {
                    "translation" => _scoringService.EvaluateTranslation(request.UserAnswer, exercise.Content),
                    "grammar" => _scoringService.EvaluateGrammar(request.UserAnswer, exercise.Content),
                    "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
                    _ => throw new ArgumentException($"Tipo de ejercicio desconocido: {exercise.Type}")
                };

                await _progressRepo.UpdateExerciseScoreAsync(userId, id, result.Score);

                if (result.Score < 70)
                {
                    await TrackMistakeFromStaticExercise(userId, exercise, request.UserAnswer);
                }

                return Ok(new
                {
                    result.Score,
                    result.Feedback,
                    correct = result.Score >= 70,
                    message = result.Score >= 70 ? "¡Bien hecho!" : "Sigue practicando"
                });
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar respuesta para ejercicio {Id}", id);
                throw;
            }
        }

        [HttpGet("{id}/hint")]
        public async Task<IActionResult> GetHint(int id)
        {
            try
            {
                if (id >= DYNAMIC_ID_OFFSET)
                    return Ok(new { hint = "Pista no disponible para ejercicios dinámicos" });

                var exercise = await _exerciseRepo.GetByIdAsync(id);
                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });

                string hint = exercise.Type switch
                {
                    "translation" or "pronunciation" => ExtractHint(exercise),
                    _ => "Revisa la conjugación del verbo"
                };

                return Ok(new { hint });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pista para ejercicio {Id}", id);
                throw;
            }
        }

        private async Task<object> EvaluateDynamicExercise(ExerciseSubmitDto request, Guid userId)
        {
            var type = request.ExerciseType ?? "translation";
            var content = request.ExerciseContent ?? "{}";

            var result = type switch
            {
                "translation" => _scoringService.EvaluateTranslation(request.UserAnswer, content),
                "grammar" => _scoringService.EvaluateGrammar(request.UserAnswer, content),
                "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
                _ => throw new ArgumentException($"Tipo de ejercicio desconocido: {type}")
            };

            if (result.Score < 70)
            {
                await TrackMistakeFromContent(userId, type, content, request.UserAnswer);
            }

            return new
            {
                result.Score,
                result.Feedback,
                correct = result.Score >= 70,
                message = result.Score >= 70 ? "¡Bien hecho!" : "Sigue practicando"
            };
        }

        private async Task TrackMistakeFromStaticExercise(Guid userId, Models.Exercise exercise, string userAnswer)
        {
            try
            {
                var lesson = await _lessonRepo.GetByIdAsync(exercise.LessonId);

                var mistake = new UserMistake
                {
                    UserId = userId,
                    LanguageId = lesson?.LanguageId ?? 1,
                    MistakeText = userAnswer,
                    CorrectText = ExtractCorrectAnswer(exercise),
                    ExerciseType = exercise.Type
                };

                await _mistakeRepo.AddOrUpdateAsync(mistake);
                await _mistakeRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al guardar mistake tracking");
            }
        }

        private async Task TrackMistakeFromContent(Guid userId, string type, string content, string userAnswer)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                var correctAnswer = type switch
                {
                    "translation" => parsed?.GetValueOrDefault("answer").ToString() ?? "",
                    "grammar" => parsed?.GetValueOrDefault("correct").ToString() ?? "",
                    _ => parsed?.GetValueOrDefault("phrase").ToString() ?? ""
                };

                (int? langId, _) = await GetUserLanguageInfo(userId);
                var mistake = new UserMistake
                {
                    UserId = userId,
                    LanguageId = langId ?? 1,
                    MistakeText = userAnswer,
                    CorrectText = correctAnswer,
                    ExerciseType = type
                };

                await _mistakeRepo.AddOrUpdateAsync(mistake);
                await _mistakeRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al guardar mistake tracking dinámico");
            }
        }

        private async Task<(int?, string?)> GetUserLanguageInfo(Guid userId)
        {
            var progress = await _progressRepo.GetByUserAsync(userId);
            var langId = progress.FirstOrDefault()?.LanguageId;
            return (langId, null);
        }

        private string ExtractCorrectAnswer(Models.Exercise exercise)
        {
            try
            {
                var doc = JsonDocument.Parse(exercise.Content);
                return exercise.Type switch
                {
                    "translation" => doc.RootElement.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "",
                    "grammar" => doc.RootElement.TryGetProperty("correct", out var c) ? c.GetString() ?? "" : "",
                    "pronunciation" => doc.RootElement.TryGetProperty("phrase", out var p) ? p.GetString() ?? "" : "",
                    _ => ""
                };
            }
            catch
            {
                return "";
            }
        }

        private string ExtractHint(Models.Exercise exercise)
        {
            var doc = JsonDocument.Parse(exercise.Content);
            if (doc.RootElement.TryGetProperty("hint", out var hintProp))
                return hintProp.GetString() ?? "";
            return exercise.Type == "translation"
                ? "Piensa en el contexto de la frase"
                : "Escucha atentamente los sonidos vocálicos";
        }
    }
}
