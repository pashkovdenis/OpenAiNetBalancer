using System.Collections.Generic;
using System.Threading.Tasks;

namespace SerinaBalancer.Models
{
    public class OpenAIRequest
    {
        public string Json { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        // 💡 Новый способ получения ответа
        public TaskCompletionSource<OpenAIResponse> Reply { get; set; }


    }
}