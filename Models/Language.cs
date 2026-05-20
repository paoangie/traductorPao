namespace Api_TutorIdiomas.Models
{
    public class Language
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Inglés, Francés, etc.
        public string Code { get; set; } = string.Empty; // en, fr, es
        public string FlagIcon { get; set; } = string.Empty;
    }
}