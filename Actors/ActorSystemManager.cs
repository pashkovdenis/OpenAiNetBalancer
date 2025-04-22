using Akka.Actor;
using Akka.Configuration;
using Microsoft.Extensions.Configuration;
using SerinaBalancer.Models;
using System;
using System.Collections.Generic;

namespace SerinaBalancer.Actors
{
    public class ActorSystemManager
    {
        public ActorSystem System { get; }
        public IActorRef LoadBalancer { get; }

        public ActorSystemManager(ActorSystem system)
        {
            System = system;

            // Получаем ссылку на уже созданный актор
            var selection = System.ActorSelection("/user/loadbalancer");

            // Преобразуем в IActorRef через ResolveOne
            LoadBalancer = selection.ResolveOne(TimeSpan.FromSeconds(5)).Result;
        }
    }
}