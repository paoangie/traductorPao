using Api_TutorIdiomas.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LanguagesController : ControllerBase
    {
        private readonly ILanguageRepository _languageRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly ILogger<LanguagesController> _logger;

        public LanguagesController(
            ILanguageRepository languageRepo,
            ILessonRepository lessonRepo,
            ILogger<LanguagesController> logger)
        {
            _languageRepo = languageRepo;
            _lessonRepo = lessonRepo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var languages = await _languageRepo.GetAllAsync();
                return Ok(languages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener idiomas");
                throw;
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var language = await _languageRepo.GetByIdAsync(id);
                if (language == null)
                    return NotFound(new { error = "Idioma no encontrado" });
                return Ok(language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener idioma {Id}", id);
                throw;
            }
        }

        [HttpGet("{id}/lessons")]
        public async Task<IActionResult> GetLessons(int id)
        {
            try
            {
                var lessons = await _lessonRepo.GetByLanguageAsync(id);
                if (!lessons.Any())
                    return Ok(new { message = "No hay lecciones disponibles para este idioma", data = new List<object>() });
                return Ok(lessons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lecciones del idioma {Id}", id);
                throw;
            }
        }
    }
}
