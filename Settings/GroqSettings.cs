namespace Api_TutorIdiomas.Settings
{
    public class GroqSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string WhisperModel { get; set; } = "whisper-large-v3";
        public string LlamaModel { get; set; } = "llama3-70b-8192";
        public string ApiUrl { get; set; } = "https://api.groq.com/openai/v1";
    }
}