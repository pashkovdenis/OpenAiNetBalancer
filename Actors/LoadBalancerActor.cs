using Akka.Actor;
using Akka.Routing;
using SerinaBalancer.Controllers;
using SerinaBalancer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SerinaBalancer.Actors
{

    public class LoadBalancerActor : ReceiveActor
    {
        private class WeightedActor
        {
            public IActorRef Actor { get; set; }
            public int Weight { get; set; }
            public int Failures { get; set; } = 0;
            public string LastError { get; set; } = "";
            public DateTime LastUsedAt { get; set; } = DateTime.MinValue;

            public bool IsHealthy => Failures < 3;
        }

        private readonly List<WeightedActor> _allActors = new();
        private readonly List<WeightedActor> _localActors = new();
        private int _roundRobinIndex = 0;
        private const int MaxFailures = 3;

        public LoadBalancerActor(List<OpenAIEndpointConfig> endpoints)
        {
            
            Receive<GetStatus>(_ =>
            {
                Sender.Tell(new LoadBalancerStatus
                {
                    Endpoints = _allActors.Select(a => new EndpointInfo
                    {
                        Name = a.Actor.Path.Name,
                        Weight = a.Weight,
                        Failures = a.Failures
                    }).ToList()
                });
            });

            foreach (var ep in endpoints)
            {
                IActorRef actor;
                Props props;

                switch (ep.Type.ToLowerInvariant())
                {
                    case "ollama":
                        props = Props.Create(() => new OllamaActor(ep));
                        break;

                    case "azure":
                        props = Props.Create(() => new AzureModelActor(ep));
                        break;

                    default:
                        props = Props.Create(() => new AzureModelActor(ep)); // fallback
                        break;
                }

                // Оборачиваем в пул по MaxConcurrent
                props = props.WithRouter(new SmallestMailboxPool(ep.MaxConcurrent > 0 ? ep.MaxConcurrent : 1));
                string baseName = $"{ep.Type.ToLower()}-router-{ep.Name}";
                string uniqueName = baseName;
                int i = 1;
                while (Context.Child(uniqueName) != ActorRefs.Nobody)
                {
                    uniqueName = $"{baseName}_{i++}";
                }

                actor = Context.ActorOf(props, uniqueName);

                var wrapper = new WeightedActor
                {
                    Actor = actor,
                    Weight = ep.Weight > 0 ? ep.Weight : 1
                };

                _allActors.Add(wrapper);

                if (ep.Type.Equals("ollama", StringComparison.OrdinalIgnoreCase))
                {
                    _localActors.Add(wrapper);
                }
            }
          
            ReceiveAsync<OpenAIRequest>(async req =>
            {
                // Режим "только локальная модель"
                if (req.Headers.TryGetValue("localOnly", out var v) && v.ToLower() == "true")
                {
                    var local = GetNext(_localActors, ref _roundRobinIndex);
                    var responseLocal = await local.Actor.Ask<OpenAIResponse>(req);

                    local.LastUsedAt = DateTime.UtcNow;

                    if (responseLocal.StatusCode >= 400)
                    {
                        local.Failures++;
                        local.LastError = $"HTTP {responseLocal.StatusCode}";
                    }
                    else
                    {
                        local.Failures = 0;
                        local.LastError = "";
                    }

                    // Failover даже в локальном режиме, если хотим
                    if (responseLocal.StatusCode == 429 || responseLocal.StatusCode >= 500)
                    {
                        var fallback = GetNext(_localActors, ref _roundRobinIndex);
                        var fallbackResponse = await fallback.Actor.Ask<OpenAIResponse>(req);
                        Sender.Tell(fallbackResponse);
                    }
                    else
                    {
                        Sender.Tell(responseLocal);
                    }

                    return;
                }

                // Основной маршрут: выбираем next actor (по весу)
                var selected = GetNext(_allActors, ref _roundRobinIndex);
                var response = await selected.Actor.Ask<OpenAIResponse>(req);

                selected.LastUsedAt = DateTime.UtcNow;

                if (response.StatusCode >= 400)
                {
                    selected.Failures++;
                    selected.LastError = $"HTTP {response.StatusCode}";
                }
                else
                {
                    selected.Failures = 0;
                    selected.LastError = "";
                }

                // Failover при ошибках
                if (response.StatusCode == 429 || response.StatusCode >= 500)
                {
                    var fallback = GetNext(_allActors, ref _roundRobinIndex);
                    var fallbackResponse = await fallback.Actor.Ask<OpenAIResponse>(req);
                    Sender.Tell(fallbackResponse);
                }
                else
                {
                    Sender.Tell(response);
                }
            });
        }

        private WeightedActor GetNext(List<WeightedActor> actors, ref int index)
        {
            var valid = actors
                .Where(a => a.Failures < MaxFailures)
                .SelectMany(a => Enumerable.Repeat(a, a.Weight))
                .ToList();

            if (valid.Count == 0)
                return actors[index++ % actors.Count];

            return valid[index++ % valid.Count];
        }
    }

















}