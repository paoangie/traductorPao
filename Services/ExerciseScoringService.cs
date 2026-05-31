using System.Text.Json;

namespace Api_TutorIdiomas.Services
{
    public class ExerciseScoringService : IExerciseScoringService
    {
        private const int MinScoreForPass = 70;
        private const int PerfectScore = 100;

        private readonly GroqAiService _groqAiService;
        private readonly ILogger<ExerciseScoringService> _logger;

        public ExerciseScoringService(
            GroqAiService groqAiService,
            ILogger<ExerciseScoringService> logger)
        {
            _groqAiService = groqAiService;
            _logger = logger;
        }

        public async Task<ExerciseScoreResult> EvaluateTranslationAsync(
            string userAnswer,
            string exerciseContent,
            string languageName,
            string lessonTitle,
            string theoryContext
        )
        {
            var content = DeserializeContent(exerciseContent);
            var question = GetString(content, "question");
            var expectedAnswer = GetString(content, "answer");

            if (string.IsNullOrWhiteSpace(expectedAnswer))
                throw new ArgumentException("El ejercicio de traducción no tiene respuesta esperada");

            try
            {
                var aiResult = await _groqAiService.EvaluateTranslationAsync(
                    question,
                    userAnswer,
                    expectedAnswer,
                    languageName,
                    lessonTitle,
                    theoryContext
                );

                return new ExerciseScoreResult(aiResult.Score, aiResult.Feedback);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                _logger.LogWarning(ex, "No se pudo evaluar traducción con IA; se usará respaldo local controlado");
                var score = CalculateTranslationScore(userAnswer, expectedAnswer);
                var feedback = IsPassingScore(score)
                    ? "Respuesta aceptada por comparación local. La retroalimentación de IA no estuvo disponible."
                    : $"La respuesta no coincide con la esperada para esta lección. Respuesta esperada: {expectedAnswer}";

                return new ExerciseScoreResult(score, feedback);
            }
        }

        public async Task<ExerciseScoreResult> EvaluateGrammarAsync(
            string userAnswer,
            string exerciseContent,
            string languageName,
            string lessonTitle,
            string theoryContext
        )
        {
            var content = DeserializeContent(exerciseContent);
            var question = GetString(content, "question");
            var correctAnswer = GetString(content, "correct");

            if (string.IsNullOrWhiteSpace(correctAnswer))
                throw new ArgumentException("El ejercicio de gramática no tiene respuesta correcta");

            try
            {
                var aiResult = await _groqAiService.EvaluateGrammarAsync(
                    question,
                    userAnswer,
                    correctAnswer,
                    languageName,
                    lessonTitle,
                    theoryContext
                );

                return new ExerciseScoreResult(aiResult.Score, aiResult.Feedback);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                _logger.LogWarning(ex, "No se pudo evaluar gramática con IA; se usará respaldo local controlado");
                var score = NormalizeAnswer(userAnswer) == NormalizeAnswer(correctAnswer) ? PerfectScore : 0;
                var feedback = score == PerfectScore
                    ? "Respuesta correcta por comparación local. La retroalimentación de IA no estuvo disponible."
                    : $"La respuesta no coincide con la regla de esta lección. Respuesta esperada: {correctAnswer}";

                return new ExerciseScoreResult(score, feedback);
            }
        }

        public ExerciseScoreResult EvaluatePronunciation(
            string userAnswer,
            string? expectedPhrase
        )
        {
            var score = CalculateSimilarity(userAnswer ?? "", expectedPhrase ?? "");

            var feedback = IsPassingScore(score)
                ? "Pronunciación aceptada por similitud textual."
                : "La pronunciación reconocida no coincide suficientemente con la frase esperada.";

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
            var score = (int)((1.0 - (double)distance / longerText.Length) * PerfectScore);

            return Math.Clamp(score, 0, PerfectScore);
        }

        private static Dictionary<string, JsonElement> DeserializeContent(string exerciseContent)
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(exerciseContent)
                ?? new Dictionary<string, JsonElement>();
        }

        private static string GetString(Dictionary<string, JsonElement> content, string key)
        {
            return content.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null
                ? value.ToString()
                : string.Empty;
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
