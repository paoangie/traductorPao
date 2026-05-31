using System.Text.Json;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class TheoryService
    {
        private readonly ILessonRepository _lessonRepo;
        private readonly ILanguageRepository _languageRepo;
        private readonly ILogger<TheoryService> _logger;

        public TheoryService(
            ILessonRepository lessonRepo,
            ILanguageRepository languageRepo,
            ILogger<TheoryService> logger)
        {
            _lessonRepo = lessonRepo;
            _languageRepo = languageRepo;
            _logger = logger;
        }

        public async Task<TheoryContentDto> GetTheoryAsync(int lessonId, Guid userId)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            if (string.IsNullOrWhiteSpace(lesson.TheoryContent) || lesson.TheoryContent == "{}")
                throw new ArgumentException("La teoría de esta lección aún no está configurada");

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var theory = JsonSerializer.Deserialize<TheoryContentDto>(lesson.TheoryContent, options);

                if (theory == null || theory.Sections == null || theory.Sections.Count == 0)
                    throw new ArgumentException("La teoría de esta lección está incompleta");

                return NormalizeTheory(theory, lesson.Title);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "TheoryContent inválido para la lección {LessonId}", lessonId);
                throw new ArgumentException("La teoría de esta lección tiene un formato inválido");
            }
        }

        public async Task<string> GetTheoryContextTextAsync(int lessonId)
        {
            var lesson = await _lessonRepo.GetByIdAsync(lessonId);
            if (lesson == null)
                throw new ArgumentException("Lección no encontrada");

            var theory = await GetTheoryAsync(lessonId, Guid.Empty);
            var language = await _languageRepo.GetByIdAsync(lesson.LanguageId);
            var languageName = language?.Name ?? "idioma";

            return BuildTheoryContextText(theory, languageName, lesson.Id, lesson.Title);
        }

        public static TheoryContentDto NormalizeTheory(TheoryContentDto theory, string lessonTitle)
        {
            if (string.IsNullOrWhiteSpace(theory.Title))
                theory.Title = lessonTitle;

            theory.Sections ??= new List<TheorySectionDto>();

            foreach (var section in theory.Sections)
            {
                section.Type = section.Type?.Trim() ?? string.Empty;

                switch (section.Type)
                {
                    case "vocabulary":
                        section.Items ??= new List<VocabularyItemDto>();
                        break;
                    case "grammar":
                        section.Rules ??= new List<GrammarRuleDto>();
                        break;
                    case "phrases":
                        if (section.Phrases == null && section.Items != null)
                        {
                            section.Phrases = section.Items.Select(i => new PhraseItemDto
                            {
                                Phrase = i.Term,
                                Translation = i.Translation,
                                Context = i.Example
                            }).ToList();
                            section.Items = null;
                        }
                        section.Phrases ??= new List<PhraseItemDto>();
                        break;
                }
            }

            return theory;
        }

        public static bool HasEnoughContentForPractice(TheoryContentDto theory)
        {
            var vocabularyCount = theory.Sections
                .Where(s => s.Type == "vocabulary")
                .SelectMany(s => s.Items ?? new List<VocabularyItemDto>())
                .Count(i => !string.IsNullOrWhiteSpace(i.Term));

            var grammarCount = theory.Sections
                .Where(s => s.Type == "grammar")
                .SelectMany(s => s.Rules ?? new List<GrammarRuleDto>())
                .Count(r => !string.IsNullOrWhiteSpace(r.Explanation));

            var phraseCount = theory.Sections
                .Where(s => s.Type == "phrases")
                .SelectMany(s => s.Phrases ?? new List<PhraseItemDto>())
                .Count(p => !string.IsNullOrWhiteSpace(p.Phrase));

            return vocabularyCount > 0 && grammarCount > 0 && phraseCount > 0;
        }

        public static string BuildTheoryContextText(
            TheoryContentDto theory,
            string languageName,
            int lessonId,
            string lessonTitle)
        {
            var vocabulary = theory.Sections
                .Where(s => s.Type == "vocabulary")
                .SelectMany(s => s.Items ?? new List<VocabularyItemDto>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Term))
                .Select(i => $"- {i.Term} = {i.Translation}. Ejemplo: {i.Example}");

            var grammar = theory.Sections
                .Where(s => s.Type == "grammar")
                .SelectMany(s => s.Rules ?? new List<GrammarRuleDto>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Explanation))
                .Select(r => $"- {r.Explanation}. Ejemplos: {string.Join("; ", r.Examples)}. Consejo: {r.Tip}");

            var phrases = theory.Sections
                .Where(s => s.Type == "phrases")
                .SelectMany(s => s.Phrases ?? new List<PhraseItemDto>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Phrase))
                .Select(p => $"- {p.Phrase} = {p.Translation}. Contexto: {p.Context}");

            var culturalNotes = theory.Sections
                .Where(s => s.Type == "cultural_note" && !string.IsNullOrWhiteSpace(s.Content))
                .Select(s => $"- {s.Content}");

            return $@"Idioma: {languageName}
LessonId: {lessonId}
Lección: {lessonTitle}
Título de teoría: {theory.Title}
Introducción: {theory.Introduction}
Vocabulario:
{string.Join("\n", vocabulary)}
Gramática:
{string.Join("\n", grammar)}
Frases útiles:
{string.Join("\n", phrases)}
Notas culturales:
{string.Join("\n", culturalNotes)}
Resumen: {theory.Summary}";
        }
    }
}
