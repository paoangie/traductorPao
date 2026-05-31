using System.Text.Json;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class DynamicExerciseService
    {
        private static readonly string[] RequiredExerciseOrder = { "translation", "grammar", "pronunciation" };

        private readonly GroqAiService _groqAiService;
        private readonly IMistakeRepository _mistakeRepo;
        private readonly ILessonRepository _lessonRepo;
        private readonly ILanguageRepository _languageRepo;
        private readonly ILogger<DynamicExerciseService> _logger;

        public DynamicExerciseService(
            GroqAiService groqAiService,
            IMistakeRepository mistakeRepo,
            ILessonRepository lessonRepo,
            ILanguageRepository languageRepo,
                        ILogger<DynamicExerciseService> logger)
        {
            _groqAiService = groqAiService;
            _mistakeRepo = mistakeRepo;
            _lessonRepo = lessonRepo;
            _languageRepo = languageRepo;
            _logger = logger;
        }

        public async Task<List<DynamicExerciseDto>> GenerateExercisesAsync(int lessonId, Guid userId, int count = 3)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            if (language == null)
                throw new InvalidOperationException("La lección no tiene un idioma válido asociado");

            var theory = ParseTheory(lesson.TheoryContent, lesson.Title);
            if (!TheoryService.HasEnoughContentForPractice(theory))
                throw new InvalidOperationException("La lección no tiene teoría suficiente para generar ejercicios");

            var theoryContext = TheoryService.BuildTheoryContextText(theory, language.Name, lesson.Id, lesson.Title);
            var mistakesText = await GetMistakesTextAsync(userId, lesson.LanguageId);
            var prompt = BuildPrompt(lesson.Id, lesson.LanguageId, language.Name, lesson.Title, lesson.Level, theoryContext, mistakesText, count);

            var response = await _groqAiService.GenerateExercisesJsonAsync(prompt);
            return ParseExercisesResponse(response, lesson.Id, lesson.Title, lesson.LanguageId, language.Name, theory);
        }

        public async Task<string> GetTheoryContextForLessonAsync(int lessonId)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            if (language == null)
                throw new InvalidOperationException("La lección no tiene un idioma válido asociado");

            var theory = ParseTheory(lesson.TheoryContent, lesson.Title);
            return TheoryService.BuildTheoryContextText(theory, language.Name, lesson.Id, lesson.Title);
        }

        private static TheoryContentDto ParseTheory(string theoryContent, string lessonTitle)
        {
            if (string.IsNullOrWhiteSpace(theoryContent) || theoryContent == "{}")
                throw new InvalidOperationException("La lección no tiene teoría registrada");

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var theory = JsonSerializer.Deserialize<TheoryContentDto>(theoryContent, options);
                if (theory == null || theory.Sections == null || theory.Sections.Count == 0)
                    throw new InvalidOperationException("La teoría de la lección está incompleta");

                return TheoryService.NormalizeTheory(theory, lessonTitle);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("La teoría de la lección tiene JSON inválido", ex);
            }
        }

        private static string BuildPrompt(
            int lessonId,
            int languageId,
            string languageName,
            string lessonTitle,
            int level,
            string theoryContext,
            string mistakesText,
            int count)
        {
            return $@"Eres el tutor inteligente de PaoLingua. Genera exactamente {count} ejercicios para práctica.

Contexto obligatorio:
- languageId: {languageId}
- idioma: {languageName}
- lessonId: {lessonId}
- lección: {lessonTitle}
- nivel: {level}
- errores frecuentes del estudiante: {mistakesText}

Teoría real registrada de esta lección:
{theoryContext}

Reglas estrictas:
1. Usa SOLO el idioma indicado.
2. Usa SOLO la lección indicada.
3. Usa SOLO vocabulario, gramática y frases presentes en la teoría registrada.
4. No mezcles otros idiomas ni temas de otras lecciones.
5. Si no puedes generar un ejercicio válido con la teoría dada, responde un array vacío.
6. No inventes frases fuera del contexto de la teoría.
7. Devuelve SOLO un array JSON válido, sin markdown ni texto adicional.
8. Incluye exactamente estos tipos y en este orden: translation, grammar, pronunciation.

Estructura obligatoria:
[
  {{""type"":""translation"",""question"":""..."",""answer"":""..."",""hint"":""..."",""lessonId"":{lessonId},""lessonTitle"":""{lessonTitle}"",""languageId"":{languageId},""languageName"":""{languageName}""}},
  {{""type"":""grammar"",""question"":""..."",""options"": [""..."",""..."",""..."",""...""],""correct"":""..."",""hint"":""..."",""lessonId"":{lessonId},""lessonTitle"":""{lessonTitle}"",""languageId"":{languageId},""languageName"":""{languageName}""}},
  {{""type"":""pronunciation"",""phrase"":""..."",""hint"":""..."",""lessonId"":{lessonId},""lessonTitle"":""{lessonTitle}"",""languageId"":{languageId},""languageName"":""{languageName}""}}
]";
        }

        private List<DynamicExerciseDto> ParseExercisesResponse(
            string response,
            int lessonId,
            string lessonTitle,
                        int languageId,
                        string languageName,
                        TheoryContentDto theory)
        {
            var cleaned = GroqAiService.CleanJson(response);

            try
            {
                var exercises = JsonSerializer.Deserialize<List<JsonElement>>(cleaned);
                if (exercises == null || exercises.Count == 0)
                    throw new InvalidOperationException("La IA no generó ejercicios válidos para esta lección");

                var parsed = exercises
                    .Select((ex, i) => ParseSingleExercise(ex, lessonId, lessonTitle, languageId, languageName, theory, i))
                    .ToList();

                ValidateExerciseSet(parsed);
                return parsed;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al parsear ejercicios generados por Groq: {Response}", response);
                throw new InvalidOperationException("La IA devolvió ejercicios con JSON inválido", ex);
            }
        }

        private DynamicExerciseDto ParseSingleExercise(
            JsonElement ex,
            int lessonId,
            string lessonTitle,
                        int languageId,
                        string languageName,
                        TheoryContentDto theory,
                        int index)
        {
            var type = ReadRequiredString(ex, "type");
            if (!RequiredExerciseOrder.Contains(type))
                throw new InvalidOperationException($"Tipo de ejercicio no permitido: {type}");

            if (ex.TryGetProperty("lessonId", out var lessonIdElement) && lessonIdElement.TryGetInt32(out var generatedLessonId) && generatedLessonId != lessonId)
                throw new InvalidOperationException("La IA generó un ejercicio para otra lección");

            if (ex.TryGetProperty("lessonTitle", out var generatedTitleElement))
            {
                var generatedTitle = generatedTitleElement.GetString();
                if (!string.IsNullOrWhiteSpace(generatedTitle) && !string.Equals(generatedTitle.Trim(), lessonTitle.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("La IA generó un ejercicio con otro título de lección");
            }

            var dto = new DynamicExerciseDto
            {
                ExerciseId = $"{lessonId}-{index + 1}",
                Type = type,
                LessonId = lessonId,
                LessonTitle = lessonTitle,
                LanguageId = languageId,
                LanguageName = languageName,
                Question = ex.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                Answer = ex.TryGetProperty("answer", out var a) ? a.GetString() : null,
                Correct = ex.TryGetProperty("correct", out var c) ? c.GetString() : null,
                Phrase = ex.TryGetProperty("phrase", out var p) ? p.GetString() : null,
                Hint = ex.TryGetProperty("hint", out var h) ? h.GetString() : ""
            };

            if (ex.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
            {
                dto.Options = opts.EnumerateArray()
                    .Select(o => o.GetString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            ValidateExercise(dto, theory);
            return dto;
        }

        private static void ValidateExerciseSet(List<DynamicExerciseDto> exercises)
        {
            if (exercises.Count != RequiredExerciseOrder.Length)
                throw new InvalidOperationException("La IA no generó la cantidad esperada de ejercicios");

            for (var i = 0; i < RequiredExerciseOrder.Length; i++)
            {
                if (exercises[i].Type != RequiredExerciseOrder[i])
                    throw new InvalidOperationException("La IA no respetó el orden requerido de ejercicios");
            }
        }

        private static void ValidateExercise(DynamicExerciseDto dto, TheoryContentDto theory)
        {
            if (string.IsNullOrWhiteSpace(dto.Hint))
                throw new InvalidOperationException("La IA generó un ejercicio sin pista");

            switch (dto.Type)
            {
                case "translation":
                    Require(dto.Question, "pregunta de traducción");
                    Require(dto.Answer, "respuesta de traducción");
                    EnsureUsesTheory(dto.Question + " " + dto.Answer, theory, "traducción");
                    break;
                case "grammar":
                    Require(dto.Question, "pregunta de gramática");
                    Require(dto.Correct, "respuesta correcta de gramática");
                    EnsureUsesTheory(dto.Question + " " + dto.Correct + " " + string.Join(" ", dto.Options ?? new List<string>()), theory, "gramática");
                    if (dto.Options == null || dto.Options.Count != 4)
                        throw new InvalidOperationException("La IA generó un ejercicio de gramática sin cuatro opciones");
                    if (!dto.Options.Any(o => string.Equals(o.Trim(), dto.Correct?.Trim(), StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("La opción correcta no está incluida en las opciones");
                    break;
                case "pronunciation":
                    Require(dto.Phrase, "frase de pronunciación");
                    EnsureUsesTheory(dto.Phrase ?? "", theory, "pronunciación");
                    break;
                default:
                    throw new InvalidOperationException($"Tipo de ejercicio no permitido: {dto.Type}");
            }
        }

        private static void EnsureUsesTheory(string generatedText, TheoryContentDto theory, string exerciseName)
        {
            var normalizedGeneratedText = Normalize(generatedText);
            var anchors = GetTheoryAnchors(theory);

            if (anchors.Count == 0)
                throw new InvalidOperationException("La teoría no contiene suficientes datos para validar ejercicios");

            if (!anchors.Any(anchor => normalizedGeneratedText.Contains(anchor)))
                throw new InvalidOperationException($"El ejercicio de {exerciseName} no usa contenido registrado en la teoría");
        }

        private static List<string> GetTheoryAnchors(TheoryContentDto theory)
        {
            var values = new List<string>();

            foreach (var section in theory.Sections)
            {
                if (section.Items != null)
                {
                    values.AddRange(section.Items.Select(i => i.Term));
                    values.AddRange(section.Items.Select(i => i.Translation));
                    values.AddRange(section.Items.Select(i => i.Example));
                }

                if (section.Phrases != null)
                {
                    values.AddRange(section.Phrases.Select(p => p.Phrase));
                    values.AddRange(section.Phrases.Select(p => p.Translation));
                }

                if (section.Rules != null)
                    values.AddRange(section.Rules.SelectMany(r => r.Examples));
            }

            return values
                .Select(Normalize)
                .Where(v => v.Length >= 2)
                .Distinct()
                .ToList();
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static void Require(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"La IA generó un ejercicio sin {fieldName}");
        }

        private static string ReadRequiredString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                throw new InvalidOperationException($"La IA no devolvió el campo requerido '{propertyName}'");

            var value = property.GetString();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"La IA devolvió vacío el campo requerido '{propertyName}'");

            return value.Trim();
        }

        private async Task<string> GetMistakesTextAsync(Guid userId, int languageId)
        {
            var mistakes = await _mistakeRepo.GetCommonMistakesAsync(userId, languageId);
            if (mistakes == null || mistakes.Count == 0)
                return "Sin errores frecuentes registrados para este idioma";

            return string.Join("; ", mistakes.Select(m =>
                $"respuesta del estudiante: '{m.MistakeText}', respuesta esperada: '{m.CorrectText}', repeticiones: {m.Count}"));
        }
    }
}
