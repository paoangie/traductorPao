using Api_TutorIdiomas.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LessonsController : ControllerBase
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly IProgressRepository _progressRepo;
        private readonly ILogger<LessonsController> _logger;

        public LessonsController(
            ILessonRepository lessonRepo,
            IProgressRepository progressRepo,
            ILogger<LessonsController> logger)
        {
            _lessonRepo = lessonRepo;
            _progressRepo = progressRepo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var lessons = await _lessonRepo.GetAllAsync();
                return Ok(lessons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las lecciones");
                throw;
            }
        }

        [HttpGet("language/{languageId}")]
        public async Task<IActionResult> GetByLanguage(int languageId)
        {
            try
            {
                var lessons = await _lessonRepo.GetByLanguageAsync(languageId);

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdStr != null)
                {
                    var userId = Guid.Parse(userIdStr);
                    var progress = await _progressRepo.GetByUserAsync(userId);
                    var result = lessons.Select(l => new {
                        l.Id,
                        l.Title,
                        l.Level,
                        l.XpReward,
                        Completed = progress.Any(p => p.LessonId == l.Id && p.Completed),
                        Score = progress.FirstOrDefault(p => p.LessonId == l.Id)?.Score ?? 0
                    });
                    return Ok(result);
                }

                return Ok(lessons);
            }
            catch (FormatException)
            {
                return Unauthorized(new { error = "Token inválido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lecciones del idioma {LanguageId}", languageId);
                throw;
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var lesson = await _lessonRepo.GetByIdAsync(id);
                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });
                return Ok(lesson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lección {Id}", id);
                throw;
            }
        }
    }
}
