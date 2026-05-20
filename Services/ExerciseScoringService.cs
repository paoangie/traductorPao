using System.Text.Json;

namespace Api_TutorIdiomas.Services
{
    public class ExerciseScoringService
    {
        private const int MinScoreForPass = 70;

        public ExerciseScoreResult EvaluateTranslation(string userAnswer, string exerciseContent)
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, string>>(exerciseContent);
            var expectedAnswer = content?.GetValueOrDefault("answer") ?? "";

            int score = CalculateTranslationScore(userAnswer, expectedAnswer);
            string feedback = score >= MinScoreForPass
                ? "¡Correcto!"
                : "Respuesta incorrecta. Sigue practicando";

            return new ExerciseScoreResult(score, feedback);
        }

        public ExerciseScoreResult EvaluateGrammar(string userAnswer, string exerciseContent)
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, object>>(exerciseContent);
            var correctAnswer = content?.GetValueOrDefault("correct")?.ToString() ?? "";

            int score = userAnswer?.ToLower() == correctAnswer.ToLower() ? 100 : 0;
            string feedback = score == 100
                ? "¡Excelente gramatica!"
                : $"La respuesta correcta es: {correctAnswer}";

            return new ExerciseScoreResult(score, feedback);
        }

        public ExerciseScoreResult EvaluatePronunciation(string userAnswer, string? expectedPhrase)
        {
            int score = CalculateSimilarity(userAnswer ?? "", expectedPhrase ?? "");
            string feedback = score >= MinScoreForPass
                ? "Buena pronunciacion"
                : "Sigue practicando la pronunciacion";

            return new ExerciseScoreResult(score, feedback);
        }

        public int CalculateTranslationScore(string userAnswer, string expectedAnswer)
        {
            if (string.IsNullOrEmpty(userAnswer) || string.IsNullOrEmpty(expectedAnswer))
                return 0;

            userAnswer = userAnswer.ToLower().Trim();
            expectedAnswer = expectedAnswer.ToLower().Trim();

            if (userAnswer == expectedAnswer) return 100;

            var userWords = userAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var expectedWords = expectedAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (expectedWords.Length == 0) return 0;

            var matches = userWords.Count(w => expectedWords.Contains(w));
            return matches * 100 / expectedWords.Length;
        }

        public int CalculateSimilarity(string input, string expected)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expected))
                return 0;

            input = input.ToLower().Trim();
            expected = expected.ToLower().Trim();

            if (input == expected) return 100;

            var longer = input.Length > expected.Length ? input : expected;
            var shorter = input.Length > expected.Length ? expected : input;

            if (longer.Length == 0) return 100;

            int distance = LevenshteinDistance(input, expected);
            return (int)((1.0 - (double)distance / longer.Length) * 100);
        }

        private int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }

            return d[n, m];
        }
    }

    public record ExerciseScoreResult(int Score, string Feedback);
}
