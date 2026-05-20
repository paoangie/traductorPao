using System.Text.Json;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class DynamicExerciseService
    {
        private readonly GroqAiService _groqAiService;
        private readonly IMistakeRepository _mistakeRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly ILanguageRepository _languageRepo;
        private readonly IProgressRepository _progressRepo;
        private readonly ILogger<DynamicExerciseService> _logger;

        public DynamicExerciseService(
            GroqAiService groqAiService,
            IMistakeRepository mistakeRepo,
            ILessonRepository lessonRepo,
            ILanguageRepository languageRepo,
            IProgressRepository progressRepo,
            ILogger<DynamicExerciseService> logger)
        {
            _groqAiService = groqAiService;
            _mistakeRepo = mistakeRepo;
            _lessonRepo = lessonRepo;
            _languageRepo = languageRepo;
            _progressRepo = progressRepo;
            _logger = logger;
        }

        public async Task<List<DynamicExerciseDto>> GenerateExercisesAsync(int lessonId, Guid userId, int count = 3)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            var languageName = language?.Name ?? "idioma";

            var levelName = lesson.Level switch
            {
                1 => "A1 (Básico)",
                2 => "A2 (Elemental)",
                3 => "B1 (Intermedio)",
                4 => "B2 (Intermedio-Alto)",
                5 => "C1 (Avanzado)",
                6 => "C2 (Maestría)",
                _ => "A1 (Básico)"
            };

            var mistakesText = await GetMistakesTextAsync(userId, lesson.LanguageId);

            var prompt = $@"Eres un tutor de idiomas experto. Genera {count} ejercicios de práctica para {languageName}.

Contexto del estudiante:
- Idioma: {languageName}
- Nivel: {levelName}
- Tema de la lección: {lesson.Title}
- Errores frecuentes del estudiante: {mistakesText}

IMPORTANTE: Responde SOLO con un array JSON válido. Sin markdown, sin texto adicional, sin comillas al inicio/fin.

Cada objeto debe seguir estas estructuras según el tipo:

Para ""translation"":
{{""type"":""translation"",""question"":""[frase a traducir]"",""answer"":""[traducción correcta]"",""hint"":""[pista]"",""lessonTitle"":""{lesson.Title}""}}

Para ""grammar"":
{{""type"":""grammar"",""question"":""[oración con espacio en blanco o ejercicio]"",""options"":[""opcion1"",""opcion2"",""opcion3"",""opcion4""],""correct"":""[opción correcta]"",""hint"":""[pista]"",""lessonTitle"":""{lesson.Title}""}}

Para ""pronunciation"":
{{""type"":""pronunciation"",""phrase"":""[frase a pronunciar]"",""hint"":""[pista de pronunciación]"",""lessonTitle"":""{lesson.Title}""}}

Reglas:
1. Incluye UNA de cada tipo (translation, grammar, pronunciation) en ese orden.
2. El contenido debe ser apropiado para el nivel {levelName}.
3. Si el estudiante tiene errores frecuentes, incluye ejercicios que refuercen esas áreas.
4. Las frases deben ser naturales y útiles en conversación real.
5. Para grammar, asegúrate de que solo UNA opción sea correcta.";
            try
            {
                var response = await _groqAiService.QueryLlamaAsync(prompt);
                var exercises = ParseExercisesResponse(response, lessonId);
                return exercises;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar ejercicios dinámicos con Groq");
                return GetFallbackExercises(lessonId, lesson.Title, languageName, levelName);
            }
        }

        private List<DynamicExerciseDto> ParseExercisesResponse(string response, int lessonId)
        {
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
            if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
            if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
            cleaned = cleaned.Trim();

            try
            {
                var exercises = JsonSerializer.Deserialize<List<JsonElement>>(cleaned);
                if (exercises == null || exercises.Count == 0)
                    throw new JsonException("Lista vacía o nula");

                return exercises.Select((ex, i) => ParseSingleExercise(ex, lessonId, i)).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al parsear respuesta de Groq: {Response}", response);
                throw;
            }
        }

        private DynamicExerciseDto ParseSingleExercise(JsonElement ex, int lessonId, int index)
        {
            var type = ex.TryGetProperty("type", out var t) ? t.GetString() ?? "translation" : "translation";
            var dto = new DynamicExerciseDto
            {
                ExerciseId = $"{lessonId}-{index + 1}",
                Type = type,
                Question = ex.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                Answer = ex.TryGetProperty("answer", out var a) ? a.GetString() : null,
                Correct = ex.TryGetProperty("correct", out var c) ? c.GetString() : null,
                Phrase = ex.TryGetProperty("phrase", out var p) ? p.GetString() : null,
                Hint = ex.TryGetProperty("hint", out var h) ? h.GetString() : ""
            };

            if (ex.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
            {
                dto.Options = opts.EnumerateArray().Select(o => o.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            }

            return dto;
        }

        private async Task<string> GetMistakesTextAsync(Guid userId, int languageId)
        {
            var mistakes = await _mistakeRepo.GetCommonMistakesAsync(userId, languageId);
            if (mistakes == null || mistakes.Count == 0)
                return "Ninguno por ahora";

            return string.Join("; ", mistakes.Select(m =>
                $"\"{m.MistakeText}\" (debería ser \"{m.CorrectText}\", {m.Count} veces)"));
        }

        private List<DynamicExerciseDto> GetFallbackExercises(int lessonId, string lessonTitle, string languageName, string levelName)
        {
            return new List<DynamicExerciseDto>
            {
                new()
                {
                    ExerciseId = $"{lessonId}-1",
                    Type = "translation",
                    Question = $"Traduce al {languageName}: Buenos días",
                    Answer = languageName.ToLower() == "inglés" ? "Good morning" :
                             languageName.ToLower() == "francés" ? "Bonjour" :
                             languageName.ToLower() == "alemán" ? "Guten Morgen" :
                             languageName.ToLower() == "italiano" ? "Buongiorno" : "Good morning",
                    Hint = "Es un saludo matutino"
                },
                new()
                {
                    ExerciseId = $"{lessonId}-2",
                    Type = "grammar",
                    Question = $"Completa la oración: ___ {GetSamplePhrase(languageName)}",
                    Options = GetSampleOptions(languageName),
                    Correct = GetSampleCorrect(languageName),
                    Hint = "Piensa en la conjugación correcta"
                },
                new()
                {
                    ExerciseId = $"{lessonId}-3",
                    Type = "pronunciation",
                    Phrase = GetSamplePhrase(languageName),
                    Hint = "Pronuncia claramente cada sílaba"
                }
            };
        }

        private string GetSamplePhrase(string language) => language.ToLower() switch
        {
            "inglés" or "english" => "How are you today",
            "francés" or "french" => "Comment allez-vous",
            "alemán" or "german" => "Wie geht es Ihnen",
            "italiano" or "italian" => "Come stai",
            _ => "Hello, how are you"
        };

        private string GetSampleCorrect(string language) => language.ToLower() switch
        {
            "inglés" or "english" => "are",
            _ => "are"
        };

        private List<string> GetSampleOptions(string language) => language.ToLower() switch
        {
            "inglés" or "english" => new() { "is", "are", "am", "be" },
            _ => new() { "is", "are", "am", "be" }
        };
    }
}
