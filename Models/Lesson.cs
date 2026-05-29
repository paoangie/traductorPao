using System.ComponentModel.DataAnnotations.Schema;

namespace Api_TutorIdiomas.Models
{
    public class Lesson
    {
        public int Id { get; set; }
        public int LanguageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Level { get; set; }
        public int XpReward { get; set; } = 50;

        [Column(TypeName = "jsonb")]
        public string TheoryContent { get; set; } = "{}";

        public Language? Language { get; set; }
        public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    }
}