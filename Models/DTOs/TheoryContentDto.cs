namespace Api_TutorIdiomas.Models.DTOs
{
    public class TheoryContentDto
    {
        public string Title { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public List<TheorySectionDto> Sections { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    public class TheorySectionDto
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<VocabularyItemDto>? Items { get; set; }
        public List<GrammarRuleDto>? Rules { get; set; }
        public List<PhraseItemDto>? Phrases { get; set; }
        public string? Content { get; set; }
    }

    public class VocabularyItemDto
    {
        public string Term { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string Pronunciation { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
    }

    public class GrammarRuleDto
    {
        public string Explanation { get; set; } = string.Empty;
        public List<string> Examples { get; set; } = new();
        public string Tip { get; set; } = string.Empty;
    }

    public class PhraseItemDto
    {
        public string Phrase { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
    }
}
