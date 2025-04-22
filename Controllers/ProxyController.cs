using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SerinaBalancer.Actors;
using SerinaBalancer.Models;
using Akka.Actor;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System;
using Microsoft.AspNetCore.Http;
using System.Text.Unicode;
using Akka.Util;

namespace SerinaBalancer.Controllers
{
    [ApiController]
    [Route("{*path}")]
    public class ProxyController : ControllerBase
    {
        private readonly ActorSystemManager _manager;

        public ProxyController(ActorSystemManager manager)
        {
            _manager = manager;
        }

        [HttpPost]
        public async Task Post()
        {

            // development only check 
            //Request.Headers.Add("localOnly", "true");

            var localOnly = Request.Headers.TryGetValue("localOnly", out var val) && val == "true";
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            using var reader = new StreamReader(Request.Body);
            var json = await reader.ReadToEndAsync();

            var req = new OpenAIRequest
            {
                Json = json,
                Headers = headers
            };

            var isStreamRequest = json.Contains("\"stream\":true");

             var result = await _manager.LoadBalancer.Ask<OpenAIResponse>(req, TimeSpan.FromSeconds(60));


            if (isStreamRequest)
            {


                await Response.StartAsync(); 
                await result.Stream.CopyToAsync(Response.Body);
            }else
            {
                // обычный JSON-ответ
                Response.StatusCode = 200;
                Response.ContentType = "application/json";
                await result.Stream.CopyToAsync(Response.Body);
            }



        }
 

    }
}