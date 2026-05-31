using Api_TutorIdiomas.Data;
using Api_TutorIdiomas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api_TutorIdiomas.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SetupController : ControllerBase
    {
        private readonly BdContext _context;
        private readonly ILogger<SetupController> _logger;

        public SetupController(BdContext context, ILogger<SetupController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("inicializar-sistema")]
        public async Task<IActionResult> Initialize()
        {
            try
            {
                var resultados = new List<string>();

                if (!await _context.Languages.AnyAsync())
                {
                    var languages = LanguageConstants.All.Select(kv => new Language
                    {
                        Id = kv.Key,
                        Name = kv.Value.Name,
                        Code = kv.Value.Code,
                        FlagIcon = kv.Value.Flag
                    }).ToArray();

                    _context.Languages.AddRange(languages);
                    resultados.Add("Idiomas base creados");
                }

                if (!await _context.Lessons.AnyAsync())
                {
                    _context.Lessons.AddRange(GetInitialLessons());
                    resultados.Add("Lecciones base creadas sin ejercicios demo");
                }

                if (!await _context.Users.AnyAsync(u => u.Email == "admin@tutor.com"))
                {
                    var admin = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Admin",
                        Email = "admin@tutor.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123"),
                        Role = "Admin",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(admin);
                    resultados.Add("Usuario administrador de desarrollo creado");
                }

                if (!await _context.Users.AnyAsync(u => u.Email == "test@test.com"))
                {
                    var testUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Usuario de Prueba",
                        Email = "test@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                        Role = "User",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(testUser);
                    resultados.Add("Usuario de prueba de desarrollo creado");
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Sistema de PaoLingua inicializado correctamente",
                    detalles = resultados,
                    nota = "Los ejercicios se generan con IA usando la teoría registrada de cada lección. No se crean ejercicios demo quemados."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar el sistema");
                return StatusCode(500, new { error = "Error al inicializar el sistema", detalle = ex.Message });
            }
        }

        [HttpPost("llenar-ejercicios")]
        public IActionResult LlenarEjercicios()
        {
            return Conflict(new
            {
                message = "La carga de ejercicios demo está deshabilitada.",
                detalle = "Los ejercicios deben generarse con IA usando la teoría real asociada a cada lección."
            });
        }

        [HttpGet("verificar-estado")]
        public async Task<IActionResult> VerificarEstado()
        {
            try
            {
                var estado = new
                {
                    Idiomas = await _context.Languages.CountAsync(),
                    Lecciones = await _context.Lessons.CountAsync(),
                    Ejercicios = await _context.Exercises.CountAsync(),
                    Usuarios = await _context.Users.CountAsync(),
                    Progresos = await _context.UserProgress.CountAsync()
                };

                return Ok(estado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar estado del sistema");
                return StatusCode(500, new { error = "Error al verificar estado" });
            }
        }

        private static Lesson[] GetInitialLessons()
        {
            return new[]
            {
                new Lesson { Id = 1, LanguageId = LanguageConstants.Ingles, Title = "Saludos y Presentaciones", Level = 1, XpReward = 50 },
                new Lesson { Id = 2, LanguageId = LanguageConstants.Ingles, Title = "Números y Contar", Level = 1, XpReward = 50 },
                new Lesson { Id = 3, LanguageId = LanguageConstants.Ingles, Title = "Familia y Amigos", Level = 1, XpReward = 60 },

                new Lesson { Id = 4, LanguageId = LanguageConstants.Espanol, Title = "Saludos en Español", Level = 1, XpReward = 50 },
                new Lesson { Id = 8, LanguageId = LanguageConstants.Espanol, Title = "Vocabulario Básico", Level = 1, XpReward = 50 },
                new Lesson { Id = 9, LanguageId = LanguageConstants.Espanol, Title = "Verbos Esenciales", Level = 1, XpReward = 50 },

                new Lesson { Id = 5, LanguageId = LanguageConstants.Frances, Title = "Saludos en Francés", Level = 1, XpReward = 50 },
                new Lesson { Id = 10, LanguageId = LanguageConstants.Frances, Title = "Vocabulario Básico en Francés", Level = 1, XpReward = 50 },
                new Lesson { Id = 11, LanguageId = LanguageConstants.Frances, Title = "Verbos Esenciales en Francés", Level = 1, XpReward = 50 },

                new Lesson { Id = 6, LanguageId = LanguageConstants.Aleman, Title = "Saludos en Alemán", Level = 1, XpReward = 50 },
                new Lesson { Id = 12, LanguageId = LanguageConstants.Aleman, Title = "Vocabulario Básico en Alemán", Level = 1, XpReward = 50 },
                new Lesson { Id = 13, LanguageId = LanguageConstants.Aleman, Title = "Verbos Esenciales en Alemán", Level = 1, XpReward = 50 },

                new Lesson { Id = 7, LanguageId = LanguageConstants.Italiano, Title = "Saludos en Italiano", Level = 1, XpReward = 50 },
                new Lesson { Id = 14, LanguageId = LanguageConstants.Italiano, Title = "Vocabulario Básico en Italiano", Level = 1, XpReward = 50 },
                new Lesson { Id = 15, LanguageId = LanguageConstants.Italiano, Title = "Verbos Esenciales en Italiano", Level = 1, XpReward = 50 },
            };
        }
    }
}
