using System.Globalization;
using System.Text;
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
            ILogger<ExerciseScoringService> logger
        )
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
                throw new ArgumentException(
                    "El ejercicio de traducción no tiene respuesta esperada"
                );

            var localScore = CalculateTranslationScore(userAnswer, expectedAnswer);
            var isExactAnswer = IsExactMatch(userAnswer, expectedAnswer);

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

                return NormalizeAiResult(
                    aiResult.Score,
                    aiResult.Feedback,
                    isExactAnswer,
                    localScore,
                    expectedAnswer
                );
            }
            catch (Exception ex)
                when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo evaluar traducción con IA; se usará respaldo local controlado"
                );

                var feedback = IsPassingScore(localScore)
                    ? "Respuesta correcta. La evaluación local confirma que coincide con la respuesta esperada."
                    : $"La respuesta no coincide con la esperada para esta lección. Respuesta esperada: {expectedAnswer}";

                return new ExerciseScoreResult(localScore, feedback);
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
                throw new ArgumentException(
                    "El ejercicio de gramática no tiene respuesta correcta"
                );

            var isExactAnswer = IsExactMatch(userAnswer, correctAnswer);
            var localScore = isExactAnswer ? PerfectScore : 0;

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

                return NormalizeAiResult(
                    aiResult.Score,
                    aiResult.Feedback,
                    isExactAnswer,
                    localScore,
                    correctAnswer
                );
            }
            catch (Exception ex)
                when (ex is HttpRequestException or InvalidOperationException or JsonException)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo evaluar gramática con IA; se usará respaldo local controlado"
                );

                var feedback = localScore == PerfectScore
                    ? "Respuesta correcta. La evaluación local confirma que coincide con la respuesta esperada."
                    : $"La respuesta no coincide con la regla de esta lección. Respuesta esperada: {correctAnswer}";

                return new ExerciseScoreResult(localScore, feedback);
            }
        }

        public ExerciseScoreResult EvaluateTranslation(
            string userAnswer,
            string exerciseContent
        )
        {
            var content = DeserializeContent(exerciseContent);
            var expectedAnswer = GetString(content, "answer");

            if (string.IsNullOrWhiteSpace(expectedAnswer))
                throw new ArgumentException(
                    "El ejercicio de traducción no tiene respuesta esperada"
                );

            var score = CalculateTranslationScore(userAnswer, expectedAnswer);

            var feedback = IsPassingScore(score)
                ? "Respuesta correcta."
                : $"La respuesta no coincide con la esperada. Respuesta esperada: {expectedAnswer}";

            return new ExerciseScoreResult(score, feedback);
        }

        public ExerciseScoreResult EvaluateGrammar(
            string userAnswer,
            string exerciseContent
        )
        {
            var content = DeserializeContent(exerciseContent);
            var correctAnswer = GetString(content, "correct");

            if (string.IsNullOrWhiteSpace(correctAnswer))
                throw new ArgumentException(
                    "El ejercicio de gramática no tiene respuesta correcta"
                );

            var score = IsExactMatch(userAnswer, correctAnswer)
                ? PerfectScore
                : 0;

            var feedback = score == PerfectScore
                ? "Respuesta correcta."
                : $"La respuesta no coincide con la regla de esta lección. Respuesta esperada: {correctAnswer}";

            return new ExerciseScoreResult(score, feedback);
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
            var score = matches * PerfectScore / expectedWords.Length;

            return Math.Clamp(score, 0, PerfectScore);
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

        private static ExerciseScoreResult NormalizeAiResult(
            int aiScore,
            string? aiFeedback,
            bool isExactAnswer,
            int localScore,
            string expectedAnswer
        )
        {
            var feedback = string.IsNullOrWhiteSpace(aiFeedback)
                ? "Evaluación realizada correctamente."
                : aiFeedback.Trim();

            if (isExactAnswer)
            {
                return new ExerciseScoreResult(
                    PerfectScore,
                    EnsurePositiveFeedback(feedback, expectedAnswer)
                );
            }

            var normalizedScore = NormalizeScore(aiScore, feedback, localScore);

            if (FeedbackSaysCorrect(feedback) && normalizedScore < MinScoreForPass)
            {
                normalizedScore = Math.Max(localScore, MinScoreForPass);
            }

            if (localScore >= MinScoreForPass && normalizedScore < MinScoreForPass)
            {
                normalizedScore = localScore;
            }

            normalizedScore = Math.Clamp(normalizedScore, 0, PerfectScore);

            return new ExerciseScoreResult(normalizedScore, feedback);
        }

        private static int NormalizeScore(
            int rawScore,
            string feedback,
            int localScore
        )
        {
            if (rawScore <= 0)
                return Math.Clamp(localScore, 0, PerfectScore);

            if (rawScore == 1)
            {
                if (FeedbackSaysCorrect(feedback) || localScore >= MinScoreForPass)
                    return PerfectScore;

                return 10;
            }

            if (rawScore > 1 && rawScore <= 10)
                return Math.Clamp(rawScore * 10, 0, PerfectScore);

            if (rawScore > 100)
                return PerfectScore;

            return Math.Clamp(rawScore, 0, PerfectScore);
        }

        private static bool FeedbackSaysCorrect(string feedback)
        {
            var normalizedFeedback = NormalizeAnswer(feedback);

            return normalizedFeedback.Contains("correcta") ||
                   normalizedFeedback.Contains("correcto") ||
                   normalizedFeedback.Contains("bien") ||
                   normalizedFeedback.Contains("aceptada") ||
                   normalizedFeedback.Contains("accepted") ||
                   normalizedFeedback.Contains("correct");
        }

        private static string EnsurePositiveFeedback(
            string feedback,
            string expectedAnswer
        )
        {
            if (FeedbackSaysCorrect(feedback))
                return feedback;

            return $"Respuesta correcta. La respuesta esperada es: {expectedAnswer}.";
        }

        private static Dictionary<string, JsonElement> DeserializeContent(
            string exerciseContent
        )
        {
            if (string.IsNullOrWhiteSpace(exerciseContent))
                return new Dictionary<string, JsonElement>();

            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                exerciseContent
            ) ?? new Dictionary<string, JsonElement>();
        }

        private static string GetString(
            Dictionary<string, JsonElement> content,
            string key
        )
        {
            if (!content.TryGetValue(key, out var value) ||
                value.ValueKind == JsonValueKind.Null ||
                value.ValueKind == JsonValueKind.Undefined)
            {
                return string.Empty;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.ToString();
        }

        private static bool IsPassingScore(int score)
        {
            return score >= MinScoreForPass;
        }

        private static bool IsExactMatch(string? value, string? expected)
        {
            return NormalizeAnswer(value) == NormalizeAnswer(expected);
        }

        private static string NormalizeAnswer(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);

                if (category != UnicodeCategory.NonSpacingMark &&
                    !char.IsPunctuation(character))
                {
                    builder.Append(character);
                }
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace("  ", " ")
                .Trim();
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