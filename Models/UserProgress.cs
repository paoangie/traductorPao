using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_TutorIdiomas.Models
{
    public class UserProgress
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int LanguageId { get; set; }

        public int LessonId { get; set; }

        public int Score { get; set; } = 0;

        public bool Completed { get; set; } = false;

        public DateTime? CompletedAt { get; set; }

        // Navegación
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("LanguageId")]
        public Language? Language { get; set; }

        [ForeignKey("LessonId")]
        public Lesson? Lesson { get; set; }
    }
}