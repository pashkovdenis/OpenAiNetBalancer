using Akka.Actor; 
using SerinaBalancer.Models;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.IO;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
namespace SerinaBalancer.Actors
{
    public class OllamaActor : ReceiveActor
    {
        private readonly OpenAIEndpointConfig _config;
        private readonly HttpClient _http = new();
        private readonly ILogger _logger;


        public OllamaActor(OpenAIEndpointConfig config, ILogger  logger)
        {
            _config = config;
            _logger = logger;

            ReceiveAsync<OpenAIRequest>(async req =>
            {
                try
                {
                    var isStreamRequest = req.Json.Contains("\"stream\":true");
                    var jsonObj = JObject.Parse(req.Json);
                    var messages = jsonObj["messages"]?.ToObject<List<Message>>() ?? new();
                    var prompt = new StringBuilder();

                    foreach (var msg in messages)
                    {
                        prompt.AppendLine($"{msg.Role.ToUpperInvariant()}: {msg.Content}");
                    }

                    var model = config.Name ?? "llama3";

                    var ollamaRequest = new
                    {
                        model = model, 
                        messages = messages,
                        stream = isStreamRequest
                    };


                    _logger.LogWarning("OOLLAMA !!!!!!!!!!!!!!!!!!" + _config.Name);

                    if (isStreamRequest)
                    {
                        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.Url + "/api/chat")
                        {
                            Content = new StringContent(JsonSerializer.Serialize(ollamaRequest), Encoding.UTF8, "application/json"),
                        };

                        var httpResponse = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

                        var stream = await httpResponse.Content.ReadAsStreamAsync();

                        Sender.Tell(new OpenAIResponse
                        {
                            StatusCode = (int)httpResponse.StatusCode,
                            Stream = stream,
                            Headers = httpResponse.Headers
                        });
                    }else
                    {

                        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.Url + "/v1/chat/completions")
                        {
                            Content = new StringContent(JsonSerializer.Serialize(ollamaRequest), Encoding.UTF8, "application/json"),
                        };

                        var httpResponse = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

                        var stream = await httpResponse.Content.ReadAsStreamAsync();

                        Sender.Tell(new OpenAIResponse
                        {
                            StatusCode = (int)httpResponse.StatusCode,
                            Stream = stream,
                            Headers = httpResponse.Headers
                        });

                    }

                }
                catch (Exception ex )
                {
                    _logger.LogError(ex, "error Ollma"); 
                    var error = $"data: {{\"error\": \"{ex.Message}\"}}\n\ndata: [DONE]\n\n";
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(error));
                    Sender.Tell(new OpenAIResponse
                    {
                        StatusCode = 500,
                        Stream = stream,
                       
                    });
                } 

            }); 
        }

         
        public class Message
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }
    }
}