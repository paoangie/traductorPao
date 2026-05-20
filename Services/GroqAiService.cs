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

        public GroqAiService(HttpClient httpClient, IOptions<GroqSettings> groqSettings, ILogger<GroqAiService> logger)
        {
            _httpClient = httpClient;
            _groqSettings = groqSettings.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_groqSettings.ApiKey) || _groqSettings.ApiKey.Contains("TU_API_KEY"))
                _logger.LogWarning("Groq API Key no configurada correctamente en appsettings.json");

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_groqSettings.ApiKey}");
        }

        public async Task<string> QueryLlamaAsync(string prompt)
        {
            return await QueryAiAsync(prompt, _groqSettings.LlamaModel);
        }

        public async Task<string> QueryAiAsync(string prompt, string model)
        {
            try
            {
                var requestBody = new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = 0.7,
                    max_tokens = 500
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.PostAsync($"{_groqSettings.ApiUrl}/chat/completions", content, cts.Token);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout al consultar Groq con modelo {Model}", model);
                throw new TaskCanceledException("La consulta a Groq tardó demasiado");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error HTTP al consultar Groq con modelo {Model}: {Message}", model, ex.Message);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al parsear respuesta de Groq con modelo {Model}", model);
                throw new InvalidOperationException("Error al procesar la respuesta del asistente de IA");
            }
        }

        public async Task<string> TranscribeAudioAsync(byte[] audioData)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(audioData);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
                content.Add(fileContent, "file", "audio.webm");
                content.Add(new StringContent(_groqSettings.WhisperModel), "model");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var response = await _httpClient.PostAsync($"{_groqSettings.ApiUrl}/audio/transcriptions", content, cts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("text").GetString() ?? "";
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout al transcribir audio con Groq Whisper");
                throw new TaskCanceledException("La transcripción del audio tardó demasiado");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error HTTP al transcribir audio: {Message}", ex.Message);
                throw new HttpRequestException("Error al transcribir el audio con el servicio de IA");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error al parsear respuesta de transcripción");
                throw new InvalidOperationException("Error al procesar la transcripción del audio");
            }
        }

        public async Task<string> CorrectGrammarAsync(string originalText, string expectedText)
        {
            var prompt = $@"
Eres un tutor de idiomas. El usuario debía decir: '{expectedText}'.
Pero dijo: '{originalText}'.
Analiza la pronunciación y gramática. Da feedback amigable y sugerencias de mejora. Respuesta en español, máximo 150 palabras.";

            return await QueryLlamaAsync(prompt);
        }
    }
}
