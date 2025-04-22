using System.Collections.Generic;

namespace SerinaBalancer.Models
{
    public class OpenAIRequest
    {
        public string Json { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}