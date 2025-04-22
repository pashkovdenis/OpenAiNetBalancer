using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SerinaBalancer.Actors;
using SerinaBalancer.Models;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<SerinaBalancer.Actors.ActorSystemManager>();

builder.Services.AddHeaderPropagation();

builder.Services.AddHttpClient();

builder.Services.AddSingleton(_ =>
{
    var configStr = @"
        akka {
            loglevel = DEBUG
            loggers = [""Akka.Event.StandardOutLogger""]
        }";
    var hocon = Akka.Configuration.ConfigurationFactory.ParseString(configStr);
    var system = ActorSystem.Create("SerinaBalancerSystem", hocon);

    var config = builder.Configuration.GetSection("OpenAIEndpoints").Get<List<OpenAIEndpointConfig>>();
    system.ActorOf(Props.Create(() => new LoadBalancerActor(config)), "loadbalancer");

    return system; 

});


var app = builder.Build();


app.UseHeaderPropagation();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;

    // если путь Azure SDK, перенаправим на нормальный
    if (path != null && path.StartsWith("/openai/deployments/") && path.EndsWith("/chat/completions"))
    {
        context.Request.Path = "/chat/completions";
    }

    await next();
});

app.MapControllers();







app.Run();