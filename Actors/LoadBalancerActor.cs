using Akka.Actor;
using Microsoft.Extensions.Logging;
using SerinaBalancer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SerinaBalancer.Actors
{
    public class LoadBalancerActor : ReceiveActor
    {
        private class ModelQueue
        {
            public IActorRef Actor { get; set; }
            public Channel<OpenAIRequest> Queue { get; set; }
            public SemaphoreSlim ConcurrencyLimiter { get; set; }
            public int InFlight => _inFlight;
            private int _inFlight = 0;
            public DateTime LastUsedAt { get; set; } = DateTime.MinValue;
            public int MaxConcurrent { get; set; }

            public async Task Enqueue(OpenAIRequest req, ILogger logger)
            {
                await Queue.Writer.WriteAsync(req);
            }

            public void Increment() => Interlocked.Increment(ref _inFlight);
            public void Decrement() => Interlocked.Decrement(ref _inFlight);
        }

        private readonly List<ModelQueue> _models = new();
        private readonly ILogger _logger;

        public LoadBalancerActor(List<OpenAIEndpointConfig> endpoints, ILogger logger)
        {
            _logger = logger;

            foreach (var ep in endpoints)
            {
                var channel = Channel.CreateBounded<OpenAIRequest>(new BoundedChannelOptions(1000)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });

                var actor = Context.ActorOf(
                    ep.Type.ToLower() switch
                    {
                        "ollama" => Props.Create(() => new OllamaActor(ep, logger)),
                        "azure" => Props.Create(() => new AzureModelActor(ep, logger)),
                        _ => Props.Create(() => new AzureModelActor(ep, logger)),
                    },
                    $"{ep.Type.ToLower()}-{ep.Name}"
                );

                var model = new ModelQueue
                {
                    Actor = actor,
                    Queue = channel,
                    ConcurrencyLimiter = new SemaphoreSlim(ep.MaxConcurrent),
                    MaxConcurrent = ep.MaxConcurrent
                };

                _models.Add(model);

                for (int i = 0; i < ep.MaxConcurrent; i++)
                    StartWorker(model);
            }

            ReceiveAsync<OpenAIRequest>(async req =>
            {
                var target = _models
                  .Where(m => m.ConcurrencyLimiter.CurrentCount > 0)
                  .OrderBy(m => m.InFlight)
                  .ThenBy(m => m.LastUsedAt)
                  .FirstOrDefault();

                _logger.LogInformation($"📥 Routing to {target.Actor.Path.Name} | InFlight: {target.InFlight}");
                await target.Enqueue(req, _logger);
            });
        }

        private void StartWorker(ModelQueue model)
        {
            _ = Task.Run(async () =>
            {
                await foreach (var req in model.Queue.Reader.ReadAllAsync())
                {
                    await model.ConcurrencyLimiter.WaitAsync();
                    model.Increment();

                    _logger.LogInformation($"⚙ Executing on {model.Actor.Path.Name} | InFlight: {model.InFlight}");

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var response = await model.Actor.Ask<OpenAIResponse>(req, TimeSpan.FromSeconds(240));
                        sw.Stop();

                        _logger.LogInformation($"✅ Done in {sw.ElapsedMilliseconds}ms from {model.Actor.Path.Name}");

                        req.Reply?.TrySetResult(response);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Error in {model.Actor.Path.Name}");
                        var fallback = new OpenAIResponse
                        {
                            StatusCode = 500,
                            Stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                                $"data: {{\"error\": \"{ex.Message}\"}}\n\ndata: [DONE]\n\n"))
                        };
                        req.Reply?.TrySetResult(fallback);
                    }
                    finally
                    {
                        model.LastUsedAt = DateTime.UtcNow;
                        model.Decrement();
                        model.ConcurrencyLimiter.Release();
                        _logger.LogInformation($"📉 Released {model.Actor.Path.Name} | InFlight: {model.InFlight}");
                    }
                }
            });
        }
    }
}
