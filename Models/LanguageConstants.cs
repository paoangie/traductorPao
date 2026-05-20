namespace Api_TutorIdiomas.Models
{
    public static class LanguageConstants
    {
        public const int Ingles = 1;
        public const int Espanol = 2;
        public const int Frances = 3;
        public const int Aleman = 4;
        public const int Italiano = 5;

        public static readonly Dictionary<int, (string Name, string Code, string Flag)> All = new()
        {
            { Ingles,    ("Inglés",   "en", "🇺🇸") },
            { Espanol,   ("Español",  "es", "🇪🇸") },
            { Frances,   ("Francés",  "fr", "🇫🇷") },
            { Aleman,    ("Alemán",   "de", "🇩🇪") },
            { Italiano,  ("Italiano", "it", "🇮🇹") }
        };
    }
}
