using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PronunciationController : ControllerBase
    {
        private readonly PronunciationService _pronunciationService;
        private readonly IPronunciationRepository _pronunciationRepo;
        private readonly ILogger<PronunciationController> _logger;

        public PronunciationController(
            PronunciationService pronunciationService,
            IPronunciationRepository pronunciationRepo,
            ILogger<PronunciationController> logger)
        {
            _pronunciationService = pronunciationService;
            _pronunciationRepo = pronunciationRepo;
            _logger = logger;
        }

        [HttpPost("evaluate")]
        public async Task<IActionResult> Evaluate([FromBody] PronunciationRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var result = await _pronunciationService.EvaluatePronunciationAsync(request, userId.Value);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Datos de audio inválidos");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error de reconocimiento de audio");
                return BadRequest(new { error = ex.Message });
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Formato de audio inválido");
                return BadRequest(new { error = "El formato del audio no es válido" });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al comunicarse con Groq API");
                return StatusCode(502, new { error = "Error al procesar el audio con el servicio de IA" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al evaluar pronunciación");
                throw;
            }
        }

        [HttpPost("practice-word")]
        public async Task<IActionResult> PracticeWord([FromBody] PracticeWordRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                if (string.IsNullOrWhiteSpace(request.Word))
                    return BadRequest(new { error = "La palabra es requerida" });

                var practice = await _pronunciationService.GetWordPracticeAsync(request.Word);

                return Ok(new
                {
                    word = request.Word,
                    phoneticHint = practice.PhoneticHint,
                    tips = practice.Tips,
                    example = practice.Example,
                    feedback = practice.Feedback
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error al consultar Groq para práctica de palabra");
                return StatusCode(502, new { error = "Error al obtener retroalimentación de IA" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en practice-word");
                throw;
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var history = await _pronunciationRepo.GetByUserAsync(userId.Value);
                var recent = history.OrderByDescending(h => h.CreatedAt).Take(Math.Max(1, limit));

                return Ok(recent.Select(h => new
                {
                    h.Id,
                    h.RecognizedText,
                    h.ExpectedText,
                    h.Score,
                    h.CreatedAt,
                    h.ExerciseId
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de pronunciación");
                throw;
            }
        }

        [HttpGet("history/{exerciseId}")]
        public async Task<IActionResult> GetHistoryByExercise(int exerciseId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var history = await _pronunciationRepo.GetByExerciseAsync(exerciseId);
                var userHistory = history.Where(h => h.UserId == userId.Value);

                return Ok(userHistory.Select(h => new
                {
                    h.Id,
                    h.RecognizedText,
                    h.ExpectedText,
                    h.Score,
                    h.CreatedAt
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial por ejercicio {ExerciseId}", exerciseId);
                throw;
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var attempts = await _pronunciationRepo.GetByUserAsync(userId.Value);
                var averageScore = attempts.Any() ? attempts.Average(a => a.Score) : 0;
                var totalAttempts = attempts.Count;
                var bestScore = attempts.Any() ? attempts.Max(a => a.Score) : 0;

                var difficultWords = attempts
                    .Where(a => a.Score < 60)
                    .Select(a => a.ExpectedText)
                    .Distinct()
                    .Take(5)
                    .ToList();

                return Ok(new
                {
                    averageScore = Math.Round(averageScore, 1),
                    totalAttempts,
                    bestScore,
                    difficultWords,
                    message = averageScore >= 80 ? "¡Excelente pronunciacion!" :
                             averageScore >= 60 ? "Buen trabajo, sigue practicando" :
                             "Practica mas los sonidos dificiles"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de pronunciación");
                throw;
            }
        }

        [HttpGet("difficult-words")]
        public async Task<IActionResult> GetDifficultWords()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var attempts = await _pronunciationRepo.GetByUserAsync(userId.Value);

                var difficultWords = attempts
                    .Where(a => a.Score < 60)
                    .GroupBy(a => a.ExpectedText)
                    .Select(g => new
                    {
                        Word = g.Key,
                        AverageScore = Math.Round(g.Average(a => a.Score), 1),
                        Attempts = g.Count()
                    })
                    .OrderBy(w => w.AverageScore)
                    .Take(10)
                    .ToList();

                return Ok(difficultWords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener palabras difíciles");
                throw;
            }
        }

        private Guid? GetUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return null;
            return Guid.Parse(userIdStr);
        }

    }

    public class PracticeWordRequest
    {
        public string Word { get; set; } = string.Empty;
    }
}
