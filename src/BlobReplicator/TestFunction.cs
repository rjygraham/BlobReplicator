using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;

namespace BlobReplicator
{
    public static class TestFunction
    {
        [FunctionName("TestFunction")]
        public static async Task<IActionResult> TestFunctionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test/ip")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"Invoking {nameof(TestFunctionAsync)}");

			var response = await HttpClientFactory.Create().GetAsync("https://api.ipify.org/?format=text");

			if (response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync();
				return new OkObjectResult(body);
			}

			return new ObjectResult(null) { StatusCode = (int)HttpStatusCode.InternalServerError };
        }
    }
}
