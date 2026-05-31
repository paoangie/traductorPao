using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api_TutorIdiomas.Settings;
using Microsoft.Extensions.Options;

namespace Api_TutorIdiomas.Services
{
    public class GroqAiService
    {
        private readonly HttpClient _httpClient;
        private readonly GroqSettings _groqSettings;
        private readonly ILogger<GroqAiService> _logger;

        public GroqAiService(
            HttpClient httpClient,
            IOptions<GroqSettings> groqSettings,
            ILogger<GroqAiService> logger)
        {
            _httpClient = httpClient;
            _groqSettings = groqSettings.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_groqSettings.ApiKey))
                _logger.LogWarning("Groq API Key no configurada. Use user-secrets o variables de entorno.");
            else
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _groqSettings.ApiKey);
        }

        public Task<string> QueryLlamaAsync(string prompt)
        {
            return QueryAiAsync(prompt, _groqSettings.LlamaModel);
        }

        public Task<string> GenerateExercisesJsonAsync(string prompt)
        {
            return QueryAiAsync(prompt, _groqSettings.LlamaModel, temperature: 0.2, maxTokens: 1400);
        }

        public async Task<AiEvaluationResult> EvaluateTranslationAsync(
            string question,
            string userAnswer,
            string expectedAnswer,
            string languageName,
            string lessonTitle,
            string theoryContext)
        {
            var prompt = $@"Eres un tutor inteligente de idiomas de PaoLingua.
Evalúa una respuesta de traducción usando SOLO el contexto real de la lección.

Idioma objetivo: {languageName}
Lección: {lessonTitle}
Contexto teórico de la lección:
{theoryContext}

Pregunta del ejercicio: {question}
Respuesta esperada: {expectedAnswer}
Respuesta del estudiante: {userAnswer}

Reglas:
- Acepta pequeñas variaciones si conservan el mismo significado.
- No corrijas usando contenido de otro idioma o de otra lección.
- Da feedback breve, útil y relacionado con esta pregunta.
- Responde SOLO JSON válido con esta forma:
{{""score"":0,""feedback"":""texto breve""}}";

            var response = await QueryAiAsync(prompt, _groqSettings.LlamaModel, temperature: 0.1, maxTokens: 350);
            return ParseEvaluationResponse(response);
        }

        public async Task<AiEvaluationResult> EvaluateGrammarAsync(
            string question,
            string userAnswer,
            string correctAnswer,
            string languageName,
            string lessonTitle,
            string theoryContext)
        {
            var prompt = $@"Eres un tutor inteligente de idiomas de PaoLingua.
Evalúa una respuesta de gramática usando SOLO el contexto real de la lección.

Idioma objetivo: {languageName}
Lección: {lessonTitle}
Contexto teórico de la lección:
{theoryContext}

Pregunta del ejercicio: {question}
Respuesta correcta: {correctAnswer}
Respuesta del estudiante: {userAnswer}

Reglas:
- Valida si la respuesta del estudiante es gramaticalmente correcta para esta pregunta.
- Explica brevemente la regla aplicada.
- No uses contenido de otro idioma o de otra lección.
- Responde SOLO JSON válido con esta forma:
{{""score"":0,""feedback"":""texto breve""}}";

            var response = await QueryAiAsync(prompt, _groqSettings.LlamaModel, temperature: 0.1, maxTokens: 350);
            return ParseEvaluationResponse(response);
        }

        public async Task<AiPronunciationFeedback> GeneratePronunciationFeedbackAsync(
            string recognizedText,
            string expectedText)
        {
            var prompt = $@"Eres un tutor de pronunciación de PaoLingua.
Compara la frase esperada con el texto reconocido por Whisper.

Frase esperada: {expectedText}
Texto reconocido: {recognizedText}

Reglas:
- Da feedback amable, breve y concreto.
- Indica qué sonido o palabra mejorar si aplica.
- No inventes que el audio fue perfecto si el texto reconocido difiere.
- Responde SOLO JSON válido con esta forma:
{{""feedback"":""texto breve"",""suggestions"":""sugerencia concreta""}}";

            var response = await QueryAiAsync(prompt, _groqSettings.LlamaModel, temperature: 0.2, maxTokens: 350);
            return ParsePronunciationFeedback(response);
        }

        public async Task<WordPracticeFeedback> GenerateWordPracticeAsync(string word)
        {
            var prompt = $@"Eres un tutor de pronunciación de PaoLingua.
Genera guía para practicar esta palabra o frase: {word}

Reglas:
- No asumas un idioma si no está indicado; trabaja con el texto recibido.
- No uses ejemplos fijos externos al texto recibido.
- Responde SOLO JSON válido con esta forma:
{{""phoneticHint"":""guía fonética o pronunciación aproximada"",""tips"":""consejo breve"",""example"":""ejemplo corto usando la palabra si es posible"",""feedback"":""retroalimentación breve""}}";

            var response = await QueryAiAsync(prompt, _groqSettings.LlamaModel, temperature: 0.2, maxTokens: 450);
            return ParseWordPracticeFeedback(response);
        }

        public async Task<string> QueryAiAsync(string prompt, string model)
        {
            return await QueryAiAsync(prompt, model, temperature: 0.7, maxTokens: 500);
        }

        public async Task<string> QueryAiAsync(string prompt, string model, double temperature, int maxTokens)
        {
            EnsureApiKeyConfigured();

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature,
                max_tokens = maxTokens
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var response = await _httpClient.PostAsync(
                $"{_groqSettings.ApiUrl}/chat/completions",
                content,
                cts.Token
            );

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Error Groq Chat. Status: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    responseBody
                );

                throw new HttpRequestException(
                    $"Error al consultar IA. Status: {response.StatusCode}. Detalle: {responseBody}"
                );
            }

            using var doc = JsonDocument.Parse(responseBody);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }

        public async Task<string> TranscribeAudioAsync(byte[] audioData, string contentType = "audio/webm")
        {
            EnsureApiKeyConfigured();

            using var content = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(audioData);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var extension = ResolveAudioExtension(contentType);
            var fileName = $"audio.{extension}";

            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent(_groqSettings.WhisperModel), "model");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var response = await _httpClient.PostAsync(
                $"{_groqSettings.ApiUrl}/audio/transcriptions",
                content,
                cts.Token
            );

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Error Groq Whisper. Status: {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    responseBody
                );

                throw new HttpRequestException(
                    $"Error al transcribir audio con IA. Status: {response.StatusCode}. Detalle: {responseBody}"
                );
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("text", out var textElement))
            {
                _logger.LogError(
                    "La respuesta de transcripción no contiene el campo 'text'. Body: {Body}",
                    responseBody
                );

                throw new HttpRequestException(
                    "La respuesta de transcripción no contiene texto reconocido"
                );
            }

            return textElement.GetString() ?? "";
        }

        public async Task<string> CorrectGrammarAsync(
            string originalText,
            string expectedText
        )
        {
            var feedback = await GeneratePronunciationFeedbackAsync(originalText, expectedText);
            return feedback.Feedback;
        }

        private void EnsureApiKeyConfigured()
        {
            if (string.IsNullOrWhiteSpace(_groqSettings.ApiKey))
                throw new InvalidOperationException("Groq API Key no configurada");
        }

        private static string ResolveAudioExtension(string contentType)
        {
            var subtype = contentType.Split('/').LastOrDefault() ?? "webm";
            subtype = subtype.Split(';')[0].Replace("x-", "").Replace("+", ".");
            return string.IsNullOrWhiteSpace(subtype) ? "webm" : subtype;
        }

        private static AiEvaluationResult ParseEvaluationResponse(string response)
        {
            var cleaned = CleanJson(response);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var score = root.TryGetProperty("score", out var s) && s.TryGetInt32(out var parsedScore)
                ? Math.Clamp(parsedScore, 0, 100)
                : 0;

            var feedback = root.TryGetProperty("feedback", out var f)
                ? f.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(feedback))
                throw new JsonException("La evaluación de IA no contiene feedback");

            return new AiEvaluationResult(score, feedback);
        }

        private static AiPronunciationFeedback ParsePronunciationFeedback(string response)
        {
            var cleaned = CleanJson(response);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "";
            var suggestions = root.TryGetProperty("suggestions", out var s) ? s.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(feedback) || string.IsNullOrWhiteSpace(suggestions))
                throw new JsonException("La respuesta de pronunciación de IA está incompleta");

            return new AiPronunciationFeedback(feedback, suggestions);
        }

        private static WordPracticeFeedback ParseWordPracticeFeedback(string response)
        {
            var cleaned = CleanJson(response);
            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            return new WordPracticeFeedback(
                root.TryGetProperty("phoneticHint", out var phoneticHint) ? phoneticHint.GetString() ?? "" : "",
                root.TryGetProperty("tips", out var tips) ? tips.GetString() ?? "" : "",
                root.TryGetProperty("example", out var example) ? example.GetString() ?? "" : "",
                root.TryGetProperty("feedback", out var feedback) ? feedback.GetString() ?? "" : ""
            );
        }

        public static string CleanJson(string response)
        {
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[7..];
            if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[3..];
            if (cleaned.EndsWith("```", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[..^3];
            return cleaned.Trim();
        }
    }

    public record AiEvaluationResult(int Score, string Feedback);
    public record AiPronunciationFeedback(string Feedback, string Suggestions);
    public record WordPracticeFeedback(string PhoneticHint, string Tips, string Example, string Feedback);
}
