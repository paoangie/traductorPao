namespace Api_TutorIdiomas.Models
{
    public class PronunciationAttempt
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int ExerciseId { get; set; }
        public string AudioUrl { get; set; } = string.Empty; // Ruta o base64
        public string RecognizedText { get; set; } = string.Empty; // Lo que entendió Whisper
        public string ExpectedText { get; set; } = string.Empty;
        public int Score { get; set; } // 0-100
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Exercise? Exercise { get; set; }
    }
}