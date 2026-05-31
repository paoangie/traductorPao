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

                // 1. Inicializar Idiomas
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
                    await _context.SaveChangesAsync();
                }

                // 2. Inicializar Lecciones (6 por idioma)
                var leccionesAgregadas = 0;
                var leccionesBase = GetInitialLessons();

                foreach (var leccion in leccionesBase)
                {
                    // Evitar duplicados por LanguageId y Título
                    var existe = await _context.Lessons.AnyAsync(l =>
                        l.LanguageId == leccion.LanguageId && l.Title == leccion.Title);

                    if (!existe)
                    {
                        // Asegurar que no chocamos con IDs manuales si ya existen otros
                        leccion.Id = 0;
                        _context.Lessons.Add(leccion);
                        leccionesAgregadas++;
                    }
                }

                if (leccionesAgregadas > 0)
                {
                    await _context.SaveChangesAsync();
                    resultados.Add($"{leccionesAgregadas} lecciones base agregadas/actualizadas");
                }

                // 3. Usuarios de desarrollo
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
                    nota = "Los ejercicios se generan dinámicamente con IA usando la teoría de cada lección."
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
                detalle = "El sistema utiliza generación dinámica por IA basada en teoría para garantizar coherencia."
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

        private static List<Lesson> GetInitialLessons()
        {
            var lessons = new List<Lesson>();

            // Configuración de 6 lecciones por cada idioma en LanguageConstants
            // 1: Inglés, 2: Español, 3: Francés, 4: Alemán, 5: Italiano

            // --- INGLÉS (1) ---
            lessons.Add(new Lesson { LanguageId = 1, Title = "Greetings and Introductions", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 1, Title = "Numbers and Counting", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 1, Title = "Colors and Basic Objects", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 1, Title = "Family and People", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 1, Title = "Food and Drinks", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 1, Title = "Daily Phrases", Level = 1, XpReward = 50 });

            // --- ESPAÑOL (2) ---
            lessons.Add(new Lesson { LanguageId = 2, Title = "Saludos y Presentaciones", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 2, Title = "Números y Contar", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 2, Title = "Colores y Objetos Básicos", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 2, Title = "Familia y Personas", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 2, Title = "Comida y Bebidas", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 2, Title = "Frases Cotidianas", Level = 1, XpReward = 50 });

            // --- FRANCÉS (3) ---
            lessons.Add(new Lesson { LanguageId = 3, Title = "Salutations et Présentations", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 3, Title = "Nombres et Compter", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 3, Title = "Couleurs et Objets de Base", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 3, Title = "Famille et Personnes", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 3, Title = "Nourriture et Boissons", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 3, Title = "Phrases Quotidiennes", Level = 1, XpReward = 50 });

            // --- ALEMÁN (4) ---
            lessons.Add(new Lesson { LanguageId = 4, Title = "Begrüßung und Vorstellung", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 4, Title = "Zahlen und Zählen", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 4, Title = "Farben und Grundobjekte", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 4, Title = "Familie und Menschen", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 4, Title = "Essen und Trinken", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 4, Title = "Alltagssätze", Level = 1, XpReward = 50 });

            // --- ITALIANO (5) ---
            lessons.Add(new Lesson { LanguageId = 5, Title = "Saluti e Presentazioni", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 5, Title = "Numeri e Contare", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 5, Title = "Colori e Oggetti di Base", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 5, Title = "Famiglia e Persone", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 5, Title = "Cibo e Bevande", Level = 1, XpReward = 50 });
            lessons.Add(new Lesson { LanguageId = 5, Title = "Frasi Quotidiane", Level = 1, XpReward = 50 });

            return lessons;
        }
    }
}
