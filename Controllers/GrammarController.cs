using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

                var correction = await _groqService.CorrectGrammarAsync(request.Text, request.ExpectedText ?? "");

                return Ok(new
                {
                    originalText = request.Text,
                    correction,
                    suggestions = ExtractSuggestions(correction)
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

                var prompt = $@"
Evalúa la siguiente respuesta para un ejercicio de completar en inglés:
Frase: '{request.Sentence}'
Respuesta del usuario: '{request.UserAnswer}'
Respuesta correcta: '{request.CorrectAnswer}'

Da feedback breve (máximo 50 palabras) y una puntuación del 0 al 100.";

                var feedback = await _groqService.QueryLlamaAsync(prompt);

                return Ok(new
                {
                    correct = request.UserAnswer?.ToLower() == request.CorrectAnswer?.ToLower(),
                    feedback,
                    score = request.UserAnswer?.ToLower() == request.CorrectAnswer?.ToLower() ? 100 : 0
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

                var prompt = $@"
Evalúa la siguiente traducción:
Texto original: '{request.OriginalText}'
Traducción del usuario: '{request.UserTranslation}'
Traducción correcta: '{request.CorrectTranslation}'

Evalúa precisión (0-100) y da sugerencias de mejora.";

                var feedback = await _groqService.QueryLlamaAsync(prompt);

                return Ok(new
                {
                    score = CalculateTranslationScore(request.UserTranslation, request.CorrectTranslation),
                    feedback
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

        private int CalculateTranslationScore(string userTranslation, string correctTranslation)
        {
            if (string.IsNullOrEmpty(userTranslation) || string.IsNullOrEmpty(correctTranslation))
                return 0;

            userTranslation = userTranslation.ToLower().Trim();
            correctTranslation = correctTranslation.ToLower().Trim();

            if (userTranslation == correctTranslation) return 100;

            var userWords = userTranslation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var correctWords = correctTranslation.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (correctWords.Length == 0) return 0;

            var matches = userWords.Count(w => correctWords.Contains(w));
            return matches * 100 / correctWords.Length;
        }

        private string ExtractSuggestions(string feedback)
        {
            if (feedback.Length > 200)
                return feedback[..200] + "...";
            return feedback;
        }
    }

    public class GrammarCorrectionRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? ExpectedText { get; set; }
    }

    public class FillBlankRequest
    {
        public string Sentence { get; set; } = string.Empty;
        public string UserAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
    }

    public class TranslationRequest
    {
        public string OriginalText { get; set; } = string.Empty;
        public string UserTranslation { get; set; } = string.Empty;
        public string CorrectTranslation { get; set; } = string.Empty;
    }
}
