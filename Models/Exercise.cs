using System.ComponentModel.DataAnnotations.Schema;

namespace Api_TutorIdiomas.Models
{
    public class Exercise
    {
        public int Id { get; set; }
        public int LessonId { get; set; }
        public string Type { get; set; } = string.Empty; // "pronunciation", "translation", "grammar"

        [Column(TypeName = "jsonb")]
        public string Content { get; set; } = "{}"; // Almacena JSON dinámico

        public Lesson? Lesson { get; set; }
    }
}