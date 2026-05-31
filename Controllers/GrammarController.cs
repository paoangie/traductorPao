using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GrammarController : ControllerBase
    {
        private readonly GroqAiService _groqService;
        private readonly ILogger<GrammarController> _logger;

        public GrammarController(GroqAiService groqService, ILogger<GrammarController> logger)
        {
            _groqService = groqService;
            _logger = logger;
        }

        [HttpPost("correct")]
        public async Task<IActionResult> CorrectText([FromBody] GrammarCorrectionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Text))
                    return BadRequest(new { error = "El texto es requerido" });

                var result = await _groqService.EvaluateGrammarAsync(
                    request.Text,
                    request.Text,
                    request.ExpectedText ?? request.Text,
                    request.LanguageName ?? "idioma objetivo",
                    request.LessonTitle ?? "lección actual",
                    request.TheoryContext ?? "Contexto no proporcionado por el cliente"
                );

                return Ok(new
                {
                    originalText = request.Text,
                    correction = result.Feedback,
                    suggestions = result.Feedback,
                    score = result.Score
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al comunicarse con Groq para corrección gramatical");
                return StatusCode(502, new { error = "Error al obtener corrección del servicio de IA" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en corrección gramatical");
                throw;
            }
        }

        [HttpPost("exercise/complete")]
        public async Task<IActionResult> CompleteExercise([FromBody] FillBlankRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Sentence) || string.IsNullOrWhiteSpace(request.UserAnswer))
                    return BadRequest(new { error = "Datos incompletos para evaluar el ejercicio" });

                var result = await _groqService.EvaluateGrammarAsync(
                    request.Sentence,
                    request.UserAnswer,
                    request.CorrectAnswer,
                    request.LanguageName ?? "idioma objetivo",
                    request.LessonTitle ?? "lección actual",
                    request.TheoryContext ?? "Contexto no proporcionado por el cliente"
                );

                return Ok(new
                {
                    correct = result.Score >= 70,
                    feedback = result.Feedback,
                    score = result.Score
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al comunicarse con Groq para ejercicio de completar");
                return StatusCode(502, new { error = "Error al obtener evaluación del servicio de IA" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ejercicio de completar");
                throw;
            }
        }

        [HttpPost("exercise/translate")]
        public async Task<IActionResult> TranslateExercise([FromBody] TranslationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.UserTranslation) || string.IsNullOrWhiteSpace(request.CorrectTranslation))
                    return BadRequest(new { error = "Datos incompletos para evaluar la traducción" });

                var result = await _groqService.EvaluateTranslationAsync(
                    request.OriginalText,
                    request.UserTranslation,
                    request.CorrectTranslation,
                    request.LanguageName ?? "idioma objetivo",
                    request.LessonTitle ?? "lección actual",
                    request.TheoryContext ?? "Contexto no proporcionado por el cliente"
                );

                return Ok(new
                {
                    score = result.Score,
                    feedback = result.Feedback
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al comunicarse con Groq para traducción");
                return StatusCode(502, new { error = "Error al obtener evaluación del servicio de IA" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ejercicio de traducción");
                throw;
            }
        }
    }

    public class GrammarCorrectionRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? ExpectedText { get; set; }
        public string? LanguageName { get; set; }
        public string? LessonTitle { get; set; }
        public string? TheoryContext { get; set; }
    }

    public class FillBlankRequest
    {
        public string Sentence { get; set; } = string.Empty;
        public string UserAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public string? LanguageName { get; set; }
        public string? LessonTitle { get; set; }
        public string? TheoryContext { get; set; }
    }

    public class TranslationRequest
    {
        public string OriginalText { get; set; } = string.Empty;
        public string UserTranslation { get; set; } = string.Empty;
        public string CorrectTranslation { get; set; } = string.Empty;
        public string? LanguageName { get; set; }
        public string? LessonTitle { get; set; }
        public string? TheoryContext { get; set; }
    }
}
