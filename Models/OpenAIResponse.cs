using System.IO;
using System.Net.Http.Headers;

namespace SerinaBalancer.Models
{
    public class OpenAIResponse
    {
        public int StatusCode { get; set; } = 200;
        public Stream Stream { get; set; }
        public HttpResponseHeaders Headers { get; internal set; }
    }
}