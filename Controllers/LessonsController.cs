using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;
using Api_TutorIdiomas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LessonsController : ControllerBase
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly IProgressRepository _progressRepo;
        private readonly TheoryService _theoryService;
        private readonly ILogger<LessonsController> _logger;

        public LessonsController(
            ILessonRepository lessonRepo,
            IProgressRepository progressRepo,
            TheoryService theoryService,
            ILogger<LessonsController> logger)
        {
            _lessonRepo = lessonRepo;
            _progressRepo = progressRepo;
            _theoryService = theoryService;
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

        [HttpGet("{id}/theory")]
        public async Task<IActionResult> GetTheory(int id)
        {
            try
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Unauthorized(new { error = "Usuario no autenticado" });

                var userId = Guid.Parse(userIdStr);
                var theory = await _theoryService.GetTheoryAsync(id, userId);
                return Ok(theory);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener teoría de lección {Id}", id);
                throw;
            }
        }

        [HttpGet("admin/list")]
        public async Task<IActionResult> GetAdminList()
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role != "Admin")
                    return Forbid();

                var lessons = await _lessonRepo.GetAllAsync();

                var result = lessons.Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Level,
                    l.XpReward,
                    languageId = l.LanguageId,
                    languageName = l.Language?.Name ?? "Desconocido",
                    hasTheory = !string.IsNullOrEmpty(l.TheoryContent) && l.TheoryContent != "{}",
                    exerciseCount = l.Exercises?.Count ?? 0
                }).OrderBy(l => l.languageId).ThenBy(l => l.Level);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lista admin de lecciones");
                throw;
            }
        }

        [HttpPut("{id}/theory")]
        public async Task<IActionResult> UpdateTheory(int id, [FromBody] TheoryContentDto theoryDto)
        {
            try
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role != "Admin")
                    return Forbid();

                var lesson = await _lessonRepo.GetByIdAsync(id);
                if (lesson == null)
                    return NotFound(new { error = "Lección no encontrada" });

                var json = JsonSerializer.Serialize(theoryDto);
                lesson.TheoryContent = json;
                await _lessonRepo.UpdateAsync(lesson);
                await _lessonRepo.SaveChangesAsync();

                Console.WriteLine($"[TEORIA] ✏️ Admin actualizó teoría de lección {id} - '{lesson.Title}'");

                return Ok(new { message = "Teoría guardada correctamente", lessonId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar teoría de lección {Id}", id);
                throw;
            }
        }
    }
}
