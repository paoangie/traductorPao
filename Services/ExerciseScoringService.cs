using System.Text.Json;

namespace Api_TutorIdiomas.Services
{
    public class ExerciseScoringService : IExerciseScoringService
    {
        private const int MinScoreForPass = 70;
        private const int PerfectScore = 100;

        public ExerciseScoreResult EvaluateTranslation(
            string userAnswer,
            string exerciseContent
        )
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, string>>(
                exerciseContent
            );

            var expectedAnswer = content?.GetValueOrDefault("answer") ?? "";
            var score = CalculateTranslationScore(userAnswer, expectedAnswer);

            var feedback = IsPassingScore(score)
                ? "¡Correcto!"
                : "Respuesta incorrecta. Sigue practicando";

            return new ExerciseScoreResult(score, feedback);
        }

        public ExerciseScoreResult EvaluateGrammar(
            string userAnswer,
            string exerciseContent
        )
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, object>>(
                exerciseContent
            );

            var correctAnswer = content?.GetValueOrDefault("correct")?.ToString() ?? "";

            var score = NormalizeAnswer(userAnswer) == NormalizeAnswer(correctAnswer)
                ? PerfectScore
                : 0;

            var feedback = score == PerfectScore
                ? "¡Excelente gramática!"
                : $"La respuesta correcta es: {correctAnswer}";

            return new ExerciseScoreResult(score, feedback);
        }

        public ExerciseScoreResult EvaluatePronunciation(
            string userAnswer,
            string? expectedPhrase
        )
        {
            var score = CalculateSimilarity(userAnswer ?? "", expectedPhrase ?? "");

            var feedback = IsPassingScore(score)
                ? "Buena pronunciación"
                : "Sigue practicando la pronunciación";

            return new ExerciseScoreResult(score, feedback);
        }

        public int CalculateTranslationScore(
            string userAnswer,
            string expectedAnswer
        )
        {
            var normalizedUserAnswer = NormalizeAnswer(userAnswer);
            var normalizedExpectedAnswer = NormalizeAnswer(expectedAnswer);

            if (string.IsNullOrEmpty(normalizedUserAnswer) ||
                string.IsNullOrEmpty(normalizedExpectedAnswer))
            {
                return 0;
            }

            if (normalizedUserAnswer == normalizedExpectedAnswer)
                return PerfectScore;

            var userWords = SplitWords(normalizedUserAnswer);
            var expectedWords = SplitWords(normalizedExpectedAnswer);

            if (expectedWords.Length == 0)
                return 0;

            var matches = userWords.Count(word => expectedWords.Contains(word));

            return matches * PerfectScore / expectedWords.Length;
        }

        public int CalculateSimilarity(string input, string expected)
        {
            var normalizedInput = NormalizeAnswer(input);
            var normalizedExpected = NormalizeAnswer(expected);

            if (string.IsNullOrEmpty(normalizedInput) ||
                string.IsNullOrEmpty(normalizedExpected))
            {
                return 0;
            }

            if (normalizedInput == normalizedExpected)
                return PerfectScore;

            var longerText = normalizedInput.Length > normalizedExpected.Length
                ? normalizedInput
                : normalizedExpected;

            if (longerText.Length == 0)
                return PerfectScore;

            var distance = LevenshteinDistance(normalizedInput, normalizedExpected);

            return (int)((1.0 - (double)distance / longerText.Length) * PerfectScore);
        }

        private static bool IsPassingScore(int score)
        {
            return score >= MinScoreForPass;
        }

        private static string NormalizeAnswer(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? "";
        }

        private static string[] SplitWords(string value)
        {
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private int LevenshteinDistance(string source, string target)
        {
            int sourceLength = source.Length;
            int targetLength = target.Length;
            int[,] distance = new int[sourceLength + 1, targetLength + 1];

            if (sourceLength == 0)
                return targetLength;

            if (targetLength == 0)
                return sourceLength;

            for (int i = 0; i <= sourceLength; distance[i, 0] = i++) ;
            for (int j = 0; j <= targetLength; distance[0, j] = j++) ;

            for (int i = 1; i <= sourceLength; i++)
            {
                for (int j = 1; j <= targetLength; j++)
                {
                    int cost = target[j - 1] == source[i - 1] ? 0 : 1;

                    distance[i, j] = Math.Min(
                        Math.Min(
                            distance[i - 1, j] + 1,
                            distance[i, j - 1] + 1
                        ),
                        distance[i - 1, j - 1] + cost
                    );
                }
            }

            return distance[sourceLength, targetLength];
        }
    }

    public record ExerciseScoreResult(int Score, string Feedback);
}