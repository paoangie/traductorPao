using Api_TutorIdiomas.Models;
using Api_TutorIdiomas.Models.DTOs;
using Api_TutorIdiomas.Repositories;

namespace Api_TutorIdiomas.Services
{
    public class PronunciationService
    {
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

        public async Task<FeedbackResponse> EvaluatePronunciationAsync(PronunciationRequest request, Guid userId)
        {
            try
            {
                byte[] audioBytes;
                try
                {
                    audioBytes = Convert.FromBase64String(request.AudioBase64);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Audio Base64 inválido enviado por usuario {UserId}", userId);
                    throw new FormatException("El formato del audio no es válido");
                }

                if (audioBytes.Length == 0)
                    throw new ArgumentException("El audio está vacío");

                if (audioBytes.Length > 10 * 1024 * 1024)
                    throw new ArgumentException("El audio excede el tamaño máximo permitido (10MB)");

                var recognizedText = await _groq.TranscribeAudioAsync(audioBytes);

                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    _logger.LogWarning("Whisper devolvió texto vacío para usuario {UserId}", userId);
                    recognizedText = "(no se pudo reconocer el audio)";
                }

                var feedback = await _groq.CorrectGrammarAsync(recognizedText, request.ExpectedPhrase);
                var score = CalculateSimilarity(recognizedText.ToLower(), request.ExpectedPhrase.ToLower());

                if (request.ExerciseId < 1000000)
                {
                    var attempt = new PronunciationAttempt
                    {
                        UserId = userId,
                        ExerciseId = request.ExerciseId,
                        RecognizedText = recognizedText,
                        ExpectedText = request.ExpectedPhrase,
                        Score = score,
                        AudioUrl = "inline",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _pronunciationRepo.AddAttemptAsync(attempt);
                    await _pronunciationRepo.SaveChangesAsync();
                }

                _logger.LogInformation("Pronunciación evaluada para usuario {UserId}: score={Score}", userId, score);

                return new FeedbackResponse
                {
                    RecognizedText = recognizedText,
                    Score = score,
                    GrammarFeedback = feedback,
                    Suggestions = score >= 80 ? "¡Excelente! Sigue asi" : "Sigue practicando los sonidos dificiles"
                };
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (FormatException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al evaluar pronunciación para usuario {UserId}", userId);
                throw new InvalidOperationException("Error al procesar la evaluación de pronunciación");
            }
        }

        public async Task<string> GetWordPracticeAsync(string word)
        {
            var prompt = $@"
Eres un tutor de pronunciación. La palabra a practicar es: '{word}'.
Proporciona:
1. Desglose fonético simplificado
2. Consejos de pronunciación (máximo 3)
3. Ejemplo en una frase corta

Respuesta en español, máximo 200 palabras.";

            return await _groq.QueryLlamaAsync(prompt);
        }

        public async Task<List<PronunciationHistoryDto>> GetUserHistoryAsync(Guid userId)
        {
            var attempts = await _pronunciationRepo.GetByUserAsync(userId);

            return attempts.Select(a => new PronunciationHistoryDto
            {
                Id = a.Id,
                RecognizedText = a.RecognizedText,
                ExpectedText = a.ExpectedText,
                Score = a.Score,
                CreatedAt = a.CreatedAt,
                ExerciseId = a.ExerciseId
            }).ToList();
        }

        private int CalculateSimilarity(string recognized, string expected)
        {
            if (recognized == expected) return 100;

            var longer = recognized.Length > expected.Length ? recognized : expected;
            var shorter = recognized.Length > expected.Length ? expected : recognized;

            if (longer.Length == 0) return 100;

            var distance = LevenshteinDistance(recognized, expected);
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
