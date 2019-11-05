using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace HttpClientFactory.Polly.Demo.Controllers
{
    [Route("client")]
    public class ClientController : ControllerBase
    {
        private readonly IHttpClientFactory _clientFactory;

        public ClientController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet("Local")]
        public async Task<IActionResult> Local()
        {
            var client = _clientFactory.CreateClient("local");
            var res = await client.GetAsync("/home/delay");
            return Ok(res);
        }
        
        [HttpGet("Fanyou")]
        public async Task<IActionResult> Fanyou()
        {
            var client = _clientFactory.CreateClient("fanyou");
            var res = await client.GetAsync("/social");
            return Ok(res);
        }
    }
}