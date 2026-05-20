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
                    resultados.Add("Idiomas creados (Inglés, Español, Francés, Alemán, Italiano)");
                }

                if (!await _context.Lessons.AnyAsync())
                {
                    var lessons = new[]
                    {
                        // Inglés (3 lecciones)
                        new Lesson { Id = 1, LanguageId = LanguageConstants.Ingles, Title = "Saludos y Presentaciones", Level = 1, XpReward = 50 },
                        new Lesson { Id = 2, LanguageId = LanguageConstants.Ingles, Title = "Números y Contar", Level = 1, XpReward = 50 },
                        new Lesson { Id = 3, LanguageId = LanguageConstants.Ingles, Title = "Familia y Amigos", Level = 1, XpReward = 60 },
                        // Español (3 lecciones)
                        new Lesson { Id = 4, LanguageId = LanguageConstants.Espanol, Title = "Saludos en Español", Level = 1, XpReward = 50 },
                        new Lesson { Id = 8, LanguageId = LanguageConstants.Espanol, Title = "Vocabulario Basico", Level = 1, XpReward = 50 },
                        new Lesson { Id = 9, LanguageId = LanguageConstants.Espanol, Title = "Verbos Esenciales", Level = 1, XpReward = 50 },
                        // Francés (3 lecciones)
                        new Lesson { Id = 5, LanguageId = LanguageConstants.Frances, Title = "Bonjour! Saludos", Level = 1, XpReward = 50 },
                        new Lesson { Id = 10, LanguageId = LanguageConstants.Frances, Title = "Vocabulaire de Base", Level = 1, XpReward = 50 },
                        new Lesson { Id = 11, LanguageId = LanguageConstants.Frances, Title = "Verbes Essentiels", Level = 1, XpReward = 50 },
                        // Alemán (3 lecciones)
                        new Lesson { Id = 6, LanguageId = LanguageConstants.Aleman, Title = "Hallo! Saludos en Alemán", Level = 1, XpReward = 50 },
                        new Lesson { Id = 12, LanguageId = LanguageConstants.Aleman, Title = "Grundwortschatz", Level = 1, XpReward = 50 },
                        new Lesson { Id = 13, LanguageId = LanguageConstants.Aleman, Title = "Wichtige Verben", Level = 1, XpReward = 50 },
                        // Italiano (3 lecciones)
                        new Lesson { Id = 7, LanguageId = LanguageConstants.Italiano, Title = "Ciao! Saludos en Italiano", Level = 1, XpReward = 50 },
                        new Lesson { Id = 14, LanguageId = LanguageConstants.Italiano, Title = "Vocabolario di Base", Level = 1, XpReward = 50 },
                        new Lesson { Id = 15, LanguageId = LanguageConstants.Italiano, Title = "Verbi Essenziali", Level = 1, XpReward = 50 },
                    };
                    _context.Lessons.AddRange(lessons);
                    resultados.Add("15 lecciones creadas (3 por idioma)");
                }

                if (!await _context.Exercises.AnyAsync())
                {
                    var exercises = new[]
                    {
                        // === Inglés - Lección 1: Saludos ===
                        new Exercise { Id = 1, LessonId = 1, Type = "pronunciation", Content = @"{""phrase"":""Hello, how are you?"",""hint"":""Di: Jelo, jau ar iu?""}" },
                        new Exercise { Id = 2, LessonId = 1, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Buenos días'?"",""answer"":""Good morning""}" },
                        new Exercise { Id = 11, LessonId = 1, Type = "grammar", Content = @"{""question"":""Completa: '___ name is John'"",""correct"":""My"",""options"":[""My"",""I"",""Me"",""Mine""],""hint"":""Pronombre posesivo""}" },
                        new Exercise { Id = 12, LessonId = 1, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Gracias'?"",""answer"":""Thank you"",""hint"":""Dos palabras""}" },
                        // === Inglés - Lección 2: Números ===
                        new Exercise { Id = 3, LessonId = 2, Type = "pronunciation", Content = @"{""phrase"":""One, two, three"",""hint"":""Di: Uan, tu, zri""}" },
                        new Exercise { Id = 13, LessonId = 2, Type = "translation", Content = @"{""question"":""¿Cómo se dice el número 10?"",""answer"":""Ten"",""hint"":""Comienza con T""}" },
                        new Exercise { Id = 14, LessonId = 2, Type = "grammar", Content = @"{""question"":""¿Cuál es el plural de 'cat'?"",""correct"":""Cats"",""options"":[""Cat"",""Cats"",""Cates"",""Caties""],""hint"":""Agrega S""}" },
                        // === Inglés - Lección 3: Familia ===
                        new Exercise { Id = 15, LessonId = 3, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Madre'?"",""answer"":""Mother"",""hint"":""Otra palabra: Mom""}" },
                        new Exercise { Id = 16, LessonId = 3, Type = "pronunciation", Content = @"{""phrase"":""This is my family"",""hint"":""Di: Dis is mai famili""}" },
                        new Exercise { Id = 17, LessonId = 3, Type = "grammar", Content = @"{""question"":""Elige: 'She ___ my sister'"",""correct"":""is"",""options"":[""is"",""are"",""am"",""be""],""hint"":""Verbo to be, tercera persona""}" },
                        // === Español - Lección 4: Saludos ===
                        new Exercise { Id = 18, LessonId = 4, Type = "pronunciation", Content = @"{""phrase"":""Buenos días, ¿cómo estás?"",""hint"":""Pronuncia claro las vocales""}" },
                        new Exercise { Id = 19, LessonId = 4, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Good night'?"",""answer"":""Buenas noches"",""hint"":""Es de noche""}" },
                        new Exercise { Id = 20, LessonId = 4, Type = "grammar", Content = @"{""question"":""Completa: 'Yo ___ de Bolivia'"",""correct"":""soy"",""options"":[""soy"",""estoy"",""eres"",""es""],""hint"":""Verbo ser""}" },
                        // === Español - Lección 8: Vocabulario Básico ===
                        new Exercise { Id = 30, LessonId = 8, Type = "pronunciation", Content = @"{""phrase"":""agua, casa, sol"",""hint"":""Di: agua, casa, sol""}" },
                        new Exercise { Id = 31, LessonId = 8, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'house'?"",""answer"":""casa"",""hint"":""Empieza con c""}" },
                        new Exercise { Id = 32, LessonId = 8, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ es grande'"",""correct"":""casa"",""options"":[""casa"",""casas"",""caso"",""casi""],""hint"":""Artículo femenino""}" },
                        // === Español - Lección 9: Verbos Esenciales ===
                        new Exercise { Id = 33, LessonId = 9, Type = "pronunciation", Content = @"{""phrase"":""yo como, tu corres"",""hint"":""Di: yo como, tu corres""}" },
                        new Exercise { Id = 34, LessonId = 9, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat'?"",""answer"":""comer"",""hint"":""Termina en er""}" },
                        new Exercise { Id = 35, LessonId = 9, Type = "grammar", Content = @"{""question"":""Completa: 'Yo ___ agua'"",""correct"":""bebo"",""options"":[""bebo"",""bebes"",""bebe"",""beben""],""hint"":""Primera persona""}" },
                        // === Francés - Lección 5: Saludos ===
                        new Exercise { Id = 21, LessonId = 5, Type = "pronunciation", Content = @"{""phrase"":""Bonjour, comment allez-vous?"",""hint"":""Bonyur, coman tale vu?""}" },
                        new Exercise { Id = 22, LessonId = 5, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Thank you' en francés?"",""answer"":""Merci"",""hint"":""Mersi""}" },
                        new Exercise { Id = 23, LessonId = 5, Type = "grammar", Content = @"{""question"":""Elige el artículo correcto: '___ maison'"",""correct"":""la"",""options"":[""le"",""la"",""l''"",""les""],""hint"":""Maison es femenino""}" },
                        // === Francés - Lección 10: Vocabulaire de Base ===
                        new Exercise { Id = 36, LessonId = 10, Type = "pronunciation", Content = @"{""phrase"":""eau, maison, soleil"",""hint"":""Di: e, meson, solei""}" },
                        new Exercise { Id = 37, LessonId = 10, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en francés?"",""answer"":""eau"",""hint"":""Se pronuncia e""}" },
                        new Exercise { Id = 38, LessonId = 10, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ est belle'"",""correct"":""maison"",""options"":[""maison"",""maisons"",""maisonne"",""mason""],""hint"":""Casa en francés""}" },
                        // === Francés - Lección 11: Verbes Essentiels ===
                        new Exercise { Id = 39, LessonId = 11, Type = "pronunciation", Content = @"{""phrase"":""je mange, tu cours"",""hint"":""Di: ye manch, tu cur""}" },
                        new Exercise { Id = 40, LessonId = 11, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en francés?"",""answer"":""manger"",""hint"":""Se pronuncia manche""}" },
                        new Exercise { Id = 41, LessonId = 11, Type = "grammar", Content = @"{""question"":""Completa: 'Je ___ de l'eau'"",""correct"":""bois"",""options"":[""bois"",""boit"",""buvons"",""buvez""],""hint"":""Primera persona beber""}" },
                        // === Alemán - Lección 6: Saludos ===
                        new Exercise { Id = 24, LessonId = 6, Type = "pronunciation", Content = @"{""phrase"":""Guten Morgen, wie geht es Ihnen?"",""hint"":""Guten morgen, vi gaat es ienen?""}" },
                        new Exercise { Id = 25, LessonId = 6, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Good evening' en alemán?"",""answer"":""Guten Abend"",""hint"":""Guten Abend""}" },
                        new Exercise { Id = 26, LessonId = 6, Type = "grammar", Content = @"{""question"":""Elige: '___ heiße Peter'"",""correct"":""Ich"",""options"":[""Ich"",""Du"",""Er"",""Sie""],""hint"":""Primera persona""}" },
                        // === Alemán - Lección 12: Grundwortschatz ===
                        new Exercise { Id = 42, LessonId = 12, Type = "pronunciation", Content = @"{""phrase"":""Wasser, Haus, Sonne"",""hint"":""Di: vaser, jaus, sone""}" },
                        new Exercise { Id = 43, LessonId = 12, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en alemán?"",""answer"":""Wasser"",""hint"":""Empieza con W""}" },
                        new Exercise { Id = 44, LessonId = 12, Type = "grammar", Content = @"{""question"":""Completa: 'Das ___ ist groß'"",""correct"":""Haus"",""options"":[""Haus"",""Häuser"",""Hause"",""Hausen""],""hint"":""Casa en alemán""}" },
                        // === Alemán - Lección 13: Wichtige Verben ===
                        new Exercise { Id = 45, LessonId = 13, Type = "pronunciation", Content = @"{""phrase"":""ich esse, du läufst"",""hint"":""Di: ij ese, du loifst""}" },
                        new Exercise { Id = 46, LessonId = 13, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en alemán?"",""answer"":""essen"",""hint"":""Se pronuncia esen""}" },
                        new Exercise { Id = 47, LessonId = 13, Type = "grammar", Content = @"{""question"":""Completa: 'Ich ___ Wasser'"",""correct"":""trinke"",""options"":[""trinke"",""trinkst"",""trinkt"",""trinken""],""hint"":""Primera persona beber""}" },
                        // === Italiano - Lección 7: Saludos ===
                        new Exercise { Id = 27, LessonId = 7, Type = "pronunciation", Content = @"{""phrase"":""Buongiorno, come stai?"",""hint"":""Buonyorno, come stay?""}" },
                        new Exercise { Id = 28, LessonId = 7, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'Thank you' en italiano?"",""answer"":""Grazie"",""hint"":""Gratsie""}" },
                        new Exercise { Id = 29, LessonId = 7, Type = "grammar", Content = @"{""question"":""Completa: 'Io ___ di Roma'"",""correct"":""sono"",""options"":[""sono"",""sei"",""è"",""siamo""],""hint"":""Verbo essere, primera persona""}" },
                        // === Italiano - Lección 14: Vocabolario di Base ===
                        new Exercise { Id = 48, LessonId = 14, Type = "pronunciation", Content = @"{""phrase"":""acqua, casa, sole"",""hint"":""Di: akua, kasa, sole""}" },
                        new Exercise { Id = 49, LessonId = 14, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en italiano?"",""answer"":""acqua"",""hint"":""Empieza con a""}" },
                        new Exercise { Id = 50, LessonId = 14, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ è bella'"",""correct"":""casa"",""options"":[""casa"",""case"",""caso"",""casi""],""hint"":""Casa en italiano""}" },
                        // === Italiano - Lección 15: Verbi Essenziali ===
                        new Exercise { Id = 51, LessonId = 15, Type = "pronunciation", Content = @"{""phrase"":""io mangio, tu corri"",""hint"":""Di: io mancho, tu kori""}" },
                        new Exercise { Id = 52, LessonId = 15, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en italiano?"",""answer"":""mangiare"",""hint"":""Se pronuncia manchare""}" },
                        new Exercise { Id = 53, LessonId = 15, Type = "grammar", Content = @"{""question"":""Completa: 'Io ___ acqua'"",""correct"":""bevo"",""options"":[""bevo"",""bevi"",""beve"",""beviamo""],""hint"":""Primera persona beber""}" },
                    };
                    _context.Exercises.AddRange(exercises);
                    resultados.Add("53 ejercicios de ejemplo creados");
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
                    resultados.Add("Usuario ADMIN creado (email: admin@tutor.com, contraseña: Admin123)");
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
                    resultados.Add("Usuario TEST creado (email: test@test.com, contraseña: 123456)");
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Sistema de Tutor de Idiomas inicializado correctamente",
                    detalles = resultados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar el sistema");
                return StatusCode(500, new { error = "Error al inicializar el sistema", detalle = ex.Message });
            }
        }

        [HttpPost("llenar-ejercicios")]
        public async Task<IActionResult> LlenarEjercicios()
        {
            try
            {
                var leccionesAgregadas = 0;
                var ejerciciosAgregados = 0;

                // Lecciones faltantes (8-15)
                var leccionesFaltantes = new[]
                {
                    new Lesson { Id = 8, LanguageId = LanguageConstants.Espanol, Title = "Vocabulario Basico", Level = 1, XpReward = 50 },
                    new Lesson { Id = 9, LanguageId = LanguageConstants.Espanol, Title = "Verbos Esenciales", Level = 1, XpReward = 50 },
                    new Lesson { Id = 10, LanguageId = LanguageConstants.Frances, Title = "Vocabulaire de Base", Level = 1, XpReward = 50 },
                    new Lesson { Id = 11, LanguageId = LanguageConstants.Frances, Title = "Verbes Essentiels", Level = 1, XpReward = 50 },
                    new Lesson { Id = 12, LanguageId = LanguageConstants.Aleman, Title = "Grundwortschatz", Level = 1, XpReward = 50 },
                    new Lesson { Id = 13, LanguageId = LanguageConstants.Aleman, Title = "Wichtige Verben", Level = 1, XpReward = 50 },
                    new Lesson { Id = 14, LanguageId = LanguageConstants.Italiano, Title = "Vocabolario di Base", Level = 1, XpReward = 50 },
                    new Lesson { Id = 15, LanguageId = LanguageConstants.Italiano, Title = "Verbi Essenziali", Level = 1, XpReward = 50 },
                };

                foreach (var leccion in leccionesFaltantes)
                {
                    if (!await _context.Lessons.AnyAsync(l => l.Id == leccion.Id))
                    {
                        _context.Lessons.Add(leccion);
                        leccionesAgregadas++;
                    }
                }

                // Ejercicios faltantes (30-53)
                var ejerciciosFaltantes = new[]
                {
                    new Exercise { Id = 30, LessonId = 8, Type = "pronunciation", Content = @"{""phrase"":""agua, casa, sol"",""hint"":""Di: agua, casa, sol""}" },
                    new Exercise { Id = 31, LessonId = 8, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'house'?"",""answer"":""casa"",""hint"":""Empieza con c""}" },
                    new Exercise { Id = 32, LessonId = 8, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ es grande'"",""correct"":""casa"",""options"":[""casa"",""casas"",""caso"",""casi""],""hint"":""Artículo femenino""}" },
                    new Exercise { Id = 33, LessonId = 9, Type = "pronunciation", Content = @"{""phrase"":""yo como, tu corres"",""hint"":""Di: yo como, tu corres""}" },
                    new Exercise { Id = 34, LessonId = 9, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat'?"",""answer"":""comer"",""hint"":""Termina en er""}" },
                    new Exercise { Id = 35, LessonId = 9, Type = "grammar", Content = @"{""question"":""Completa: 'Yo ___ agua'"",""correct"":""bebo"",""options"":[""bebo"",""bebes"",""bebe"",""beben""],""hint"":""Primera persona""}" },
                    new Exercise { Id = 36, LessonId = 10, Type = "pronunciation", Content = @"{""phrase"":""eau, maison, soleil"",""hint"":""Di: e, meson, solei""}" },
                    new Exercise { Id = 37, LessonId = 10, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en francés?"",""answer"":""eau"",""hint"":""Se pronuncia e""}" },
                    new Exercise { Id = 38, LessonId = 10, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ est belle'"",""correct"":""maison"",""options"":[""maison"",""maisons"",""maisonne"",""mason""],""hint"":""Casa en francés""}" },
                    new Exercise { Id = 39, LessonId = 11, Type = "pronunciation", Content = @"{""phrase"":""je mange, tu cours"",""hint"":""Di: ye manch, tu cur""}" },
                    new Exercise { Id = 40, LessonId = 11, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en francés?"",""answer"":""manger"",""hint"":""Se pronuncia manche""}" },
                    new Exercise { Id = 41, LessonId = 11, Type = "grammar", Content = @"{""question"":""Completa: 'Je ___ de l'eau'"",""correct"":""bois"",""options"":[""bois"",""boit"",""buvons"",""buvez""],""hint"":""Primera persona beber""}" },
                    new Exercise { Id = 42, LessonId = 12, Type = "pronunciation", Content = @"{""phrase"":""Wasser, Haus, Sonne"",""hint"":""Di: vaser, jaus, sone""}" },
                    new Exercise { Id = 43, LessonId = 12, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en alemán?"",""answer"":""Wasser"",""hint"":""Empieza con W""}" },
                    new Exercise { Id = 44, LessonId = 12, Type = "grammar", Content = @"{""question"":""Completa: 'Das ___ ist groß'"",""correct"":""Haus"",""options"":[""Haus"",""Häuser"",""Hause"",""Hausen""],""hint"":""Casa en alemán""}" },
                    new Exercise { Id = 45, LessonId = 13, Type = "pronunciation", Content = @"{""phrase"":""ich esse, du läufst"",""hint"":""Di: ij ese, du loifst""}" },
                    new Exercise { Id = 46, LessonId = 13, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en alemán?"",""answer"":""essen"",""hint"":""Se pronuncia esen""}" },
                    new Exercise { Id = 47, LessonId = 13, Type = "grammar", Content = @"{""question"":""Completa: 'Ich ___ Wasser'"",""correct"":""trinke"",""options"":[""trinke"",""trinkst"",""trinkt"",""trinken""],""hint"":""Primera persona beber""}" },
                    new Exercise { Id = 48, LessonId = 14, Type = "pronunciation", Content = @"{""phrase"":""acqua, casa, sole"",""hint"":""Di: akua, kasa, sole""}" },
                    new Exercise { Id = 49, LessonId = 14, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'water' en italiano?"",""answer"":""acqua"",""hint"":""Empieza con a""}" },
                    new Exercise { Id = 50, LessonId = 14, Type = "grammar", Content = @"{""question"":""Completa: 'La ___ è bella'"",""correct"":""casa"",""options"":[""casa"",""case"",""caso"",""casi""],""hint"":""Casa en italiano""}" },
                    new Exercise { Id = 51, LessonId = 15, Type = "pronunciation", Content = @"{""phrase"":""io mangio, tu corri"",""hint"":""Di: io mancho, tu kori""}" },
                    new Exercise { Id = 52, LessonId = 15, Type = "translation", Content = @"{""question"":""¿Cómo se dice 'to eat' en italiano?"",""answer"":""mangiare"",""hint"":""Se pronuncia manchare""}" },
                    new Exercise { Id = 53, LessonId = 15, Type = "grammar", Content = @"{""question"":""Completa: 'Io ___ acqua'"",""correct"":""bevo"",""options"":[""bevo"",""bevi"",""beve"",""beviamo""],""hint"":""Primera persona beber""}" },
                };

                foreach (var ejercicio in ejerciciosFaltantes)
                {
                    if (!await _context.Exercises.AnyAsync(e => e.Id == ejercicio.Id))
                    {
                        _context.Exercises.Add(ejercicio);
                        ejerciciosAgregados++;
                    }
                }

                if (leccionesAgregadas > 0 || ejerciciosAgregados > 0)
                    await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Lecciones añadidas: {leccionesAgregadas}, Ejercicios añadidos: {ejerciciosAgregados}",
                    totalLecciones = await _context.Lessons.CountAsync(),
                    totalEjercicios = await _context.Exercises.CountAsync()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al llenar contenido adicional");
                return StatusCode(500, new { error = "Error al llenar contenido", detalle = ex.Message });
            }
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
    }
}
