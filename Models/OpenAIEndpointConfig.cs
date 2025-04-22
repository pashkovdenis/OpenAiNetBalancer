namespace SerinaBalancer.Models
{
    public class OpenAIEndpointConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string ApiKey { get; set; }

        public int MaxConcurrent { get; set; } = 1; 
        public int Weight { get; set; } = 1;
        public string Type { get; set; } // "Azure" or "Ollama"
    }
}
        