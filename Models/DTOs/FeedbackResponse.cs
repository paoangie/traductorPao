namespace Api_TutorIdiomas.Models.DTOs
{
    public class FeedbackResponse
    {
        public string RecognizedText { get; set; } = string.Empty;
        public int Score { get; set; }
        public string GrammarFeedback { get; set; } = string.Empty;
        public string Suggestions { get; set; } = string.Empty;
    }
}