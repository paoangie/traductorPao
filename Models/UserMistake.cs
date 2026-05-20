using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api_TutorIdiomas.Models
{
    public class UserMistake
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int LanguageId { get; set; }

        public string MistakeText { get; set; } = string.Empty;

        public string CorrectText { get; set; } = string.Empty;

        public string ExerciseType { get; set; } = string.Empty;

        public int Count { get; set; } = 1;

        public DateTime LastOccurrence { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("LanguageId")]
        public Language? Language { get; set; }
    }
}
