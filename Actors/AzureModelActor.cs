using Akka.Actor;
using SerinaBalancer.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace SerinaBalancer.Actors
{
    public class AzureModelActor : ReceiveActor
    {
        private readonly OpenAIEndpointConfig _config;
        private readonly HttpClient _http = new();

        public AzureModelActor(OpenAIEndpointConfig config)
        {
            _config = config;

            ReceiveAsync<OpenAIRequest>(async req =>
            {


                try
                {

                    var request = new HttpRequestMessage(HttpMethod.Post, _config.Url)
                    {
                        Content = new StringContent(req.Json, Encoding.UTF8, "application/json")
                    };

                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

                    var httpResponse = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    var stream = await httpResponse.Content.ReadAsStreamAsync();

                    Sender.Tell(new OpenAIResponse
                    {
                        StatusCode = (int)httpResponse.StatusCode,
                        Stream = stream,
                        Headers = httpResponse.Headers
                    });

                }catch (Exception ex)
                {
                    var error = $"data: {{\"error\": \"{ex.Message}\"}}\n\ndata: [DONE]\n\n";
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(error));
                    Sender.Tell(new OpenAIResponse
                    {
                        StatusCode = 500,
                        Stream = stream
                    });
                }



            });
        }
    }
}