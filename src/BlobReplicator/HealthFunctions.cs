using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.EventGrid;
using Microsoft.Azure.Management.EventGrid.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Azure.OData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlobReplicator
{
	public static class HealthFunctions
    {
		private static bool initialReady = true;
		private const string StorageAccountResourceType = "Microsoft.Storage/storageAccounts";
		private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";

		[FunctionName("HealthReadyFunction")]
        public static async Task<IActionResult> HealthReadyAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequestMessage req,
            ILogger log)
        {
			log.LogInformation("Ready probe triggered.");

			// This is a bit of a hack but necessary due to chicken/egg scenario. Kubernetes needs
			// to schedule and allow traffic to hit the pod in order for EventGrid to be able to
			// establish the WebHook subscription.
			if (initialReady)
			{
				initialReady = false;
				return new OkResult();
			}

			return await EnsureEventSubscriptions();
		}

		[FunctionName("HealthLiveFunction")]
		public static async Task<IActionResult> HealthLiveAsync(
		   [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/live")] HttpRequestMessage req,
		   ILogger log)
		{
			log.LogInformation("Liveness probe triggered.");

			return new OkResult();
		}

		private static async Task<IActionResult> EnsureEventSubscriptions()
		{
			try
			{
				var credentials = await Locator.GetTokenCredentialsAsync();
				var resourceClient = Locator.CreateResourceManagementClient(credentials);
				var eventGridClient = Locator.CreateEventGridManagementClient(credentials);

				var query = new ODataQuery<GenericResourceFilter>(f => f.ResourceType == StorageAccountResourceType);
				var storageAccounts = (await resourceClient.Resources.ListByResourceGroupAsync(Locator.ResourceGroup, query)).Where(w => w.Location.Equals(Locator.Region));
				var eventGridSubscriptions = await eventGridClient.EventSubscriptions.ListRegionalByResourceGroupAsync(Locator.ResourceGroup, Locator.Region);

				var storageAccountsMissingSubscriptions = storageAccounts.Where(w => !eventGridSubscriptions.Any(a => a.Topic.Equals(w.Id)));

				if (storageAccountsMissingSubscriptions.Count() > 0)
				{
					EventSubscription eventSubscription = new EventSubscription()
					{
						Destination = new WebHookEventSubscriptionDestination()
						{
							EndpointUrl = $"https://{Locator.Hostname}/api/blobevent"
						},
						Filter = new EventSubscriptionFilter()
						{
							IncludedEventTypes = new List<string> { BlobCreatedEventType },
							IsSubjectCaseSensitive = false,
							SubjectBeginsWith = "",
							SubjectEndsWith = "",
							AdvancedFilters = new List<AdvancedFilter> { new StringBeginsWithAdvancedFilter("data.api", new List<string> { "Put" }) }
						}
					};

					var tasks = new List<Task>();

					foreach (var storageAccount in storageAccountsMissingSubscriptions)
					{
						var name = $"{storageAccount.Name}-subscription";
						tasks.Add(eventGridClient.EventSubscriptions.CreateOrUpdateAsync(storageAccount.Id, name, eventSubscription));
					}

					await Task.WhenAll(tasks);
				}

				return new OkResult();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
				return new StatusCodeResult(500);
			}
		}

	}
}
