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
        private const int PRACTICE_EXERCISE_COUNT = 5;
        private const int MIN_SCORE_FOR_PASS = 70;
        private const int FIRST_ATTEMPT_POINTS = 20;
        private const int RETRY_ATTEMPT_POINTS = 10;

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
                var userId = GetAuthenticatedUserId();

                var lesson = await _lessonRepo.GetByIdAsync(lessonId);

                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);

                if (language == null)
                    return BadRequest(new { error = "La lección no tiene un idioma válido asociado" });

                var dynamicExercises = await _dynamicExerciseService.GenerateExercisesAsync(
                    lessonId,
                    userId,
                    PRACTICE_EXERCISE_COUNT
                );

                var result = dynamicExercises.Select((exercise, index) => new
                {
                    id = DYNAMIC_ID_OFFSET + lessonId * 100 + index,
                    lessonId,
                    languageId = lesson.LanguageId,
                    languageName = language.Name,
                    type = exercise.Type,
                    content = (object)BuildContent(exercise)
                }).ToList();

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
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
                _logger.LogError(
                    ex,
                    "Error al generar ejercicios con IA para la lección {LessonId}",
                    lessonId
                );

                return StatusCode(
                    502,
                    new { error = "Error al generar ejercicios con el servicio de IA" }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al generar ejercicios para la lección {LessonId}",
                    lessonId
                );

                throw;
            }
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
        public async Task<IActionResult> SubmitAnswer(
            int id,
            [FromBody] ExerciseSubmitDto request
        )
        {
            try
            {
                var userId = GetAuthenticatedUserId();

                if (id >= DYNAMIC_ID_OFFSET ||
                    !string.IsNullOrWhiteSpace(request.ExerciseType))
                {
                    var dynamicResult = await EvaluateDynamicExercise(id, request, userId);
                    return Ok(dynamicResult);
                }

                var exercise = await _exerciseRepo.GetByIdAsync(id);

                if (exercise == null)
                    return NotFound(new { error = "Ejercicio no encontrado" });

                var lesson = await _lessonRepo.GetByIdAsync(exercise.LessonId);

                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);

                if (language == null)
                    return BadRequest(new { error = "La lección no tiene un idioma válido asociado" });

                var theoryContext = await _dynamicExerciseService.GetTheoryContextForLessonAsync(
                    lesson.Id
                );

                var result = exercise.Type switch
                {
                    "translation" => await _scoringService.EvaluateTranslationAsync(
                        request.UserAnswer,
                        exercise.Content,
                        language.Name,
                        lesson.Title,
                        theoryContext
                    ),

                    "grammar" => await _scoringService.EvaluateGrammarAsync(
                        request.UserAnswer,
                        exercise.Content,
                        language.Name,
                        lesson.Title,
                        theoryContext
                    ),

                    "pronunciation" => _scoringService.EvaluatePronunciation(
                        request.UserAnswer,
                        request.ExpectedPhrase
                    ),

                    _ => throw new ArgumentException(
                        $"Tipo de ejercicio desconocido: {exercise.Type}"
                    )
                };

                var expectedAnswer = ExtractCorrectAnswer(exercise);
                var shouldRetry = !IsCorrect(result.Score);

                await _progressRepo.UpdateExerciseScoreAsync(
                    userId,
                    id,
                    result.Score
                );

                if (shouldRetry)
                {
                    await TrackMistake(
                        userId,
                        lesson.LanguageId,
                        exercise.Type,
                        request.UserAnswer,
                        expectedAnswer
                    );
                }

                return Ok(BuildSubmitPayload(
                    result,
                    expectedAnswer,
                    shouldRetry,
                    request.AttemptNumber
                ));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
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
                _logger.LogError(
                    ex,
                    "Error de IA al enviar respuesta para ejercicio {Id}",
                    id
                );

                return StatusCode(
                    502,
                    new { error = "Error al evaluar la respuesta con el servicio de IA" }
                );
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

        private async Task<object> EvaluateDynamicExercise(
            int id,
            ExerciseSubmitDto request,
            Guid userId
        )
        {
            var lessonId = request.LessonId ?? TryGetLessonIdFromDynamicId(id);

            if (lessonId == null)
                throw new ArgumentException(
                    "No se pudo determinar la lección del ejercicio dinámico"
                );

            var lesson = await _lessonRepo.GetByIdAsync(lessonId.Value);

            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            if (request.LanguageId.HasValue &&
                request.LanguageId.Value != lesson.LanguageId)
            {
                throw new ArgumentException(
                    "El idioma enviado no coincide con el idioma de la lección"
                );
            }

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);

            if (language == null)
                throw new InvalidOperationException(
                    "La lección no tiene un idioma válido asociado"
                );

            var exerciseType =
                request.ExerciseType ??
                ExtractTypeFromContent(request.ExerciseContent) ??
                "translation";

            var exerciseContent = request.ExerciseContent ?? "{}";

            var theoryContext = await _dynamicExerciseService.GetTheoryContextForLessonAsync(
                lesson.Id
            );

            var result = exerciseType switch
            {
                "translation" => await _scoringService.EvaluateTranslationAsync(
                    request.UserAnswer,
                    exerciseContent,
                    language.Name,
                    lesson.Title,
                    theoryContext
                ),

                "grammar" => await _scoringService.EvaluateGrammarAsync(
                    request.UserAnswer,
                    exerciseContent,
                    language.Name,
                    lesson.Title,
                    theoryContext
                ),

                "pronunciation" => _scoringService.EvaluatePronunciation(
                    request.UserAnswer,
                    request.ExpectedPhrase
                ),

                _ => throw new ArgumentException(
                    $"Tipo de ejercicio desconocido: {exerciseType}"
                )
            };

            var expectedAnswer = ExtractCorrectAnswer(
                exerciseType,
                exerciseContent,
                request.ExpectedPhrase
            );

            var shouldRetry = !IsCorrect(result.Score);

            if (shouldRetry)
            {
                await TrackMistake(
                    userId,
                    lesson.LanguageId,
                    exerciseType,
                    request.UserAnswer,
                    expectedAnswer
                );
            }

            return BuildSubmitPayload(
                result,
                expectedAnswer,
                shouldRetry,
                request.AttemptNumber
            );
        }

        private static object BuildContent(DynamicExerciseDto exercise)
        {
            var content = new Dictionary<string, object?>
            {
                ["type"] = exercise.Type,
                ["hint"] = exercise.Hint,
                ["lessonId"] = exercise.LessonId,
                ["lessonTitle"] = exercise.LessonTitle,
                ["languageId"] = exercise.LanguageId,
                ["languageName"] = exercise.LanguageName
            };

            switch (exercise.Type)
            {
                case "translation":
                    content["question"] = exercise.Question;
                    content["answer"] = exercise.Answer;
                    break;

                case "grammar":
                    content["question"] = exercise.Question;
                    content["correct"] = exercise.Correct;
                    content["options"] = exercise.Options ?? new List<string>();
                    break;

                case "pronunciation":
                    content["phrase"] = exercise.Phrase;
                    break;
            }

            return content;
        }

        private static object BuildSubmitPayload(
            ExerciseScoreResult result,
            string expectedAnswer,
            bool shouldRetry,
            int attemptNumber = 1
        )
        {
            var correct = IsCorrect(result.Score);

            var pointsEarned = correct
                ? attemptNumber <= 1
                    ? FIRST_ATTEMPT_POINTS
                    : RETRY_ATTEMPT_POINTS
                : 0;

            return new
            {
                score = result.Score,
                feedback = result.Feedback,
                correct,
                expectedAnswer,
                shouldRetry,
                attemptNumber,
                pointsEarned,
                message = correct
                    ? attemptNumber <= 1
                        ? "Respuesta aceptada"
                        : "Respuesta corregida"
                    : "Respuesta no aceptada. Este ejercicio debe repetirse."
            };
        }

        private Guid GetAuthenticatedUserId()
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdValue))
                throw new UnauthorizedAccessException("Usuario no autenticado");

            return Guid.Parse(userIdValue);
        }

        private static bool IsCorrect(int score)
        {
            return score >= MIN_SCORE_FOR_PASS;
        }

        private async Task TrackMistake(
            Guid userId,
            int languageId,
            string type,
            string userAnswer,
            string correctAnswer
        )
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
                using var document = JsonDocument.Parse(content);

                return document.RootElement.TryGetProperty("type", out var type)
                    ? type.GetString()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractCorrectAnswer(Models.Exercise exercise)
        {
            return ExtractCorrectAnswer(
                exercise.Type,
                exercise.Content,
                null
            );
        }

        private static string ExtractCorrectAnswer(
            string type,
            string content,
            string? expectedPhrase
        )
        {
            try
            {
                using var document = JsonDocument.Parse(content);

                return type switch
                {
                    "translation" => document.RootElement.TryGetProperty("answer", out var answer)
                        ? answer.GetString() ?? ""
                        : "",

                    "grammar" => document.RootElement.TryGetProperty("correct", out var correct)
                        ? correct.GetString() ?? ""
                        : "",

                    "pronunciation" => expectedPhrase ??
                        (document.RootElement.TryGetProperty("phrase", out var phrase)
                            ? phrase.GetString() ?? ""
                            : ""),

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
            try
            {
                using var document = JsonDocument.Parse(exercise.Content);

                return document.RootElement.TryGetProperty("hint", out var hint)
                    ? hint.GetString() ?? ""
                    : "";
            }
            catch
            {
                return "";
            }
        }
    }
}