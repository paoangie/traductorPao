namespace Api_TutorIdiomas.Models.DTOs
{
    public class DynamicExerciseDto
    {
        public string ExerciseId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string? Answer { get; set; }
        public string? Correct { get; set; }
        public List<string>? Options { get; set; }
        public string? Phrase { get; set; }
        public string? Hint { get; set; }
    }
}
