using Api_TutorIdiomas.Models;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class PronunciationService
    {
        private const int MaxAudioSizeBytes = 10 * 1024 * 1024;
        private const int DynamicExerciseIdLimit = 1000000;

        private readonly GroqAiService _groq;
        private readonly IPronunciationRepository _pronunciationRepo;
        private readonly ILogger<PronunciationService> _logger;

        public PronunciationService(
            GroqAiService groq,
            IPronunciationRepository pronunciationRepo,
            ILogger<PronunciationService> logger)
        {
            _groq = groq;
            _pronunciationRepo = pronunciationRepo;
            _logger = logger;
        }

        public async Task<FeedbackResponse> EvaluatePronunciationAsync(
            PronunciationRequest request,
            Guid userId
        )
        {
            var (audioBytes, contentType) = DecodeAudioBase64(request.AudioBase64);
            ValidateAudio(audioBytes);

            var detectedContentType = DetectAudioContentType(audioBytes) ?? contentType;
            var expectedPhrase = request.ExpectedPhrase?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(expectedPhrase))
                throw new ArgumentException("La frase esperada no puede estar vacía");

            var recognizedText = await _groq.TranscribeAudioAsync(audioBytes, detectedContentType);

            if (string.IsNullOrWhiteSpace(recognizedText))
                throw new InvalidOperationException("La IA no pudo reconocer texto en el audio grabado");

            var score = CalculateSimilarity(recognizedText, expectedPhrase);
            var aiFeedback = await _groq.GeneratePronunciationFeedbackAsync(recognizedText, expectedPhrase);

            if (request.ExerciseId < DynamicExerciseIdLimit)
            {
                var attempt = new PronunciationAttempt
                {
                    UserId = userId,
                    ExerciseId = request.ExerciseId,
                    RecognizedText = recognizedText,
                    ExpectedText = expectedPhrase,
                    Score = score,
                    AudioUrl = "inline",
                    CreatedAt = DateTime.UtcNow
                };

                await _pronunciationRepo.AddAttemptAsync(attempt);
                await _pronunciationRepo.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Pronunciación evaluada con IA para usuario {UserId}: score={Score}",
                userId,
                score
            );

            return new FeedbackResponse
            {
                RecognizedText = recognizedText,
                Score = score,
                GrammarFeedback = aiFeedback.Feedback,
                Suggestions = aiFeedback.Suggestions
            };
        }

        public Task<WordPracticeFeedback> GetWordPracticeAsync(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("La palabra es requerida");

            return _groq.GenerateWordPracticeAsync(word.Trim());
        }

        public async Task<List<PronunciationHistoryDto>> GetUserHistoryAsync(Guid userId)
        {
            var attempts = await _pronunciationRepo.GetByUserAsync(userId);

            return attempts.Select(attempt => new PronunciationHistoryDto
            {
                Id = attempt.Id,
                RecognizedText = attempt.RecognizedText,
                ExpectedText = attempt.ExpectedText,
                Score = attempt.Score,
                CreatedAt = attempt.CreatedAt,
                ExerciseId = attempt.ExerciseId
            }).ToList();
        }

        private static (byte[] AudioBytes, string ContentType) DecodeAudioBase64(string audioBase64)
        {
            if (string.IsNullOrWhiteSpace(audioBase64))
                throw new ArgumentException("El audio está vacío");

            var cleanBase64 = audioBase64;
            var contentType = "audio/webm";

            if (audioBase64.Contains(','))
            {
                var parts = audioBase64.Split(',', 2);
                cleanBase64 = parts[1];

                if (parts[0].StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var header = parts[0][5..];
                    var typePart = header.Split(';')[0];
                    if (!string.IsNullOrWhiteSpace(typePart))
                        contentType = typePart;
                }
            }

            return (Convert.FromBase64String(cleanBase64), contentType);
        }

        private static string? DetectAudioContentType(byte[] audioBytes)
        {
            if (audioBytes.Length >= 4)
            {
                if (audioBytes[0] == 0x1A && audioBytes[1] == 0x45 && audioBytes[2] == 0xDF && audioBytes[3] == 0xA3)
                    return "audio/webm";

                if (audioBytes[0] == 'O' && audioBytes[1] == 'g' && audioBytes[2] == 'g' && audioBytes[3] == 'S')
                    return "audio/ogg";

                if (audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')
                    return "audio/wav";

                if (audioBytes[0] == 'I' && audioBytes[1] == 'D' && audioBytes[2] == '3')
                    return "audio/mpeg";
            }

            if (audioBytes.Length >= 12 && audioBytes[4] == 'f' && audioBytes[5] == 't' && audioBytes[6] == 'y' && audioBytes[7] == 'p')
                return "audio/mp4";

            return null;
        }

        private static void ValidateAudio(byte[] audioBytes)
        {
            if (audioBytes.Length == 0)
                throw new ArgumentException("El audio está vacío");

            if (audioBytes.Length > MaxAudioSizeBytes)
                throw new ArgumentException("El audio excede el tamaño máximo permitido");
        }

        private static int CalculateSimilarity(string recognized, string expected)
        {
            var normalizedRecognized = Normalize(recognized);
            var normalizedExpected = Normalize(expected);

            if (normalizedRecognized == normalizedExpected) return 100;

            var longer = normalizedRecognized.Length > normalizedExpected.Length
                ? normalizedRecognized
                : normalizedExpected;

            if (longer.Length == 0) return 100;

            var distance = LevenshteinDistance(normalizedRecognized, normalizedExpected);
            var score = (int)((1.0 - (double)distance / longer.Length) * 100);

            return Math.Clamp(score, 0, 100);
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static int LevenshteinDistance(string source, string target)
        {
            int sourceLength = source.Length;
            int targetLength = target.Length;
            int[,] distance = new int[sourceLength + 1, targetLength + 1];

            if (sourceLength == 0) return targetLength;
            if (targetLength == 0) return sourceLength;

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

    public class PronunciationHistoryDto
    {
        public int Id { get; set; }
        public string RecognizedText { get; set; } = string.Empty;
        public string ExpectedText { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ExerciseId { get; set; }
    }
}
