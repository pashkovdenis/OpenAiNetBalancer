using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SerinaBalancer.Controllers
{
    [ApiController]
    [Route("monitor")]
    public class MonitorController : ControllerBase
    {
        private readonly ActorSystem _system;

        public MonitorController(ActorSystem system)
        {
            _system = system;
        }

        [HttpGet("status")]
        public async Task<LoadBalancerStatus> GetStatus()
        {
            var lb = _system.ActorSelection("/user/loadbalancer");
            var result = await lb.Ask<LoadBalancerStatus>(new GetStatus());
            return result;
        }
    }

    public record GetStatus();

    public class LoadBalancerStatus
    {
        public List<EndpointInfo> Endpoints { get; set; } = new();
    }

    public class EndpointInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Weight { get; set; }
        public int Failures { get; set; }
    }
}
