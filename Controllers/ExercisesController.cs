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
        private readonly ILanguageRepository _languageRepo;
        private readonly IMistakeRepository _mistakeRepo;
        private readonly IExerciseScoringService _scoringService;
        private readonly DynamicExerciseService _dynamicExerciseService;
        private readonly ILogger<ExercisesController> _logger;

        public ExercisesController(
            IExerciseRepository exerciseRepo,
            IProgressRepository progressRepo,
            ILessonRepository lessonRepo,
            ILanguageRepository languageRepo,
            IMistakeRepository mistakeRepo,
            IExerciseScoringService scoringService,
            DynamicExerciseService dynamicExerciseService,
            ILogger<ExercisesController> logger)
        {
            _exerciseRepo = exerciseRepo;
            _progressRepo = progressRepo;
            _lessonRepo = lessonRepo;
            _languageRepo = languageRepo;
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

                var lesson = await _lessonRepo.GetByIdAsync(lessonId);
                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
                if (language == null)
                    return BadRequest(new { error = "La lección no tiene un idioma válido asociado" });

                var userId = Guid.Parse(userIdStr);
                var dynamicExercises = await _dynamicExerciseService.GenerateExercisesAsync(lessonId, userId, 3);

                var result = dynamicExercises.Select((ex, i) => new
                {
                    id = DYNAMIC_ID_OFFSET + lessonId * 100 + i,
                    lessonId,
                    languageId = lesson.LanguageId,
                    languageName = language.Name,
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
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al generar ejercicios con IA para la lección {LessonId}", lessonId);
                return StatusCode(502, new { error = "Error al generar ejercicios con el servicio de IA" });
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
                ["hint"] = ex.Hint,
                ["lessonId"] = ex.LessonId,
                ["lessonTitle"] = ex.LessonTitle,
                ["languageId"] = ex.LanguageId,
                ["languageName"] = ex.LanguageName
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
                    return Ok(await EvaluateDynamicExercise(id, request, userId));

                var exercise = await _exerciseRepo.GetByIdAsync(id);
                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });

                var lesson = await _lessonRepo.GetByIdAsync(exercise.LessonId);
                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
                if (language == null)
                    return BadRequest(new { error = "La lección no tiene un idioma válido asociado" });

                var theoryContext = await _dynamicExerciseService.GetTheoryContextForLessonAsync(lesson.Id);
                var result = exercise.Type switch
                {
                    "translation" => await _scoringService.EvaluateTranslationAsync(request.UserAnswer, exercise.Content, language.Name, lesson.Title, theoryContext),
                    "grammar" => await _scoringService.EvaluateGrammarAsync(request.UserAnswer, exercise.Content, language.Name, lesson.Title, theoryContext),
                    "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
                    _ => throw new ArgumentException($"Tipo de ejercicio desconocido: {exercise.Type}")
                };

                await _progressRepo.UpdateExerciseScoreAsync(userId, id, result.Score);

                if (result.Score < 70)
                    await TrackMistake(userId, lesson.LanguageId, exercise.Type, request.UserAnswer, ExtractCorrectAnswer(exercise));

                return BuildSubmitResponse(result);
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de IA al enviar respuesta para ejercicio {Id}", id);
                return StatusCode(502, new { error = "Error al evaluar la respuesta con el servicio de IA" });
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
                    return Ok(new { hint = "Pista incluida en el contenido del ejercicio dinámico" });

                var exercise = await _exerciseRepo.GetByIdAsync(id);
                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });

                var hint = ExtractHint(exercise);
                return Ok(new { hint });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener pista para ejercicio {Id}", id);
                throw;
            }
        }

        private async Task<object> EvaluateDynamicExercise(int id, ExerciseSubmitDto request, Guid userId)
        {
            var lessonId = request.LessonId ?? TryGetLessonIdFromDynamicId(id);
            if (lessonId == null)
                throw new ArgumentException("No se pudo determinar la lección del ejercicio dinámico");

            var lesson = await _lessonRepo.GetByIdAsync(lessonId.Value);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            if (request.LanguageId.HasValue && request.LanguageId.Value != lesson.LanguageId)
                throw new ArgumentException("El idioma enviado no coincide con el idioma de la lección");

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            if (language == null)
                throw new InvalidOperationException("La lección no tiene un idioma válido asociado");

            var type = request.ExerciseType ?? ExtractTypeFromContent(request.ExerciseContent) ?? "translation";
            var content = request.ExerciseContent ?? "{}";
            var theoryContext = await _dynamicExerciseService.GetTheoryContextForLessonAsync(lesson.Id);

            var result = type switch
            {
                "translation" => await _scoringService.EvaluateTranslationAsync(request.UserAnswer, content, language.Name, lesson.Title, theoryContext),
                "grammar" => await _scoringService.EvaluateGrammarAsync(request.UserAnswer, content, language.Name, lesson.Title, theoryContext),
                "pronunciation" => _scoringService.EvaluatePronunciation(request.UserAnswer, request.ExpectedPhrase),
                _ => throw new ArgumentException($"Tipo de ejercicio desconocido: {type}")
            };

            if (result.Score < 70)
                await TrackMistake(userId, lesson.LanguageId, type, request.UserAnswer, ExtractCorrectAnswer(type, content, request.ExpectedPhrase));

            return BuildSubmitPayload(result);
        }

        private static IActionResult BuildSubmitResponse(ExerciseScoreResult result)
        {
            return new OkObjectResult(BuildSubmitPayload(result));
        }

        private static object BuildSubmitPayload(ExerciseScoreResult result)
        {
            return new
            {
                result.Score,
                result.Feedback,
                correct = result.Score >= 70,
                message = result.Score >= 70 ? "Respuesta aceptada" : "Respuesta no aceptada"
            };
        }

        private async Task TrackMistake(Guid userId, int languageId, string type, string userAnswer, string correctAnswer)
        {
            try
            {
                var mistake = new UserMistake
                {
                    UserId = userId,
                    LanguageId = languageId,
                    MistakeText = userAnswer,
                    CorrectText = correctAnswer,
                    ExerciseType = type
                };

                await _mistakeRepo.AddOrUpdateAsync(mistake);
                await _mistakeRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al guardar mistake tracking");
            }
        }

        private static int? TryGetLessonIdFromDynamicId(int id)
        {
            if (id < DYNAMIC_ID_OFFSET)
                return null;

            return (id - DYNAMIC_ID_OFFSET) / 100;
        }

        private static string? ExtractTypeFromContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                var doc = JsonDocument.Parse(content);
                return doc.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractCorrectAnswer(Models.Exercise exercise)
        {
            return ExtractCorrectAnswer(exercise.Type, exercise.Content, null);
        }

        private static string ExtractCorrectAnswer(string type, string content, string? expectedPhrase)
        {
            try
            {
                var doc = JsonDocument.Parse(content);
                return type switch
                {
                    "translation" => doc.RootElement.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "",
                    "grammar" => doc.RootElement.TryGetProperty("correct", out var c) ? c.GetString() ?? "" : "",
                    "pronunciation" => expectedPhrase ?? (doc.RootElement.TryGetProperty("phrase", out var p) ? p.GetString() ?? "" : ""),
                    _ => ""
                };
            }
            catch
            {
                return expectedPhrase ?? string.Empty;
            }
        }

        private static string ExtractHint(Models.Exercise exercise)
        {
            var doc = JsonDocument.Parse(exercise.Content);
            return doc.RootElement.TryGetProperty("hint", out var hintProp)
                ? hintProp.GetString() ?? ""
                : "";
        }
    }
}
