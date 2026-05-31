using System.ComponentModel.DataAnnotations;

namespace Api_TutorIdiomas.Models.DTOs
{
    public class ExerciseSubmitDto
    {
        [Required(ErrorMessage = "La respuesta del usuario es requerida")]
        public string UserAnswer { get; set; } = string.Empty;

        public string? ExpectedPhrase { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El tiempo debe ser un número positivo")]
        public int TimeSpentSeconds { get; set; }

        public string? ExerciseType { get; set; }

        public string? ExerciseContent { get; set; }

        public int? LessonId { get; set; }

        public int? LanguageId { get; set; }
    }
}
