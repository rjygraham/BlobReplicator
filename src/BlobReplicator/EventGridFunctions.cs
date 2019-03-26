using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace BlobReplicator
{
	public static class EventGridFunctions
	{
		[FunctionName("BlobEvent")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", "blob/event")]HttpRequestMessage req, ILogger log)
		{
			var messages = await req.Content.ReadAsAsync<IList<EventGridEvent>>();

			// If the request is for subscription validation, send back the validation code.
			if (messages.Count > 0 && string.Equals((string)messages[0].EventType, "Microsoft.EventGrid.SubscriptionValidationEvent", StringComparison.OrdinalIgnoreCase))
			{
				log.LogInformation("Validate request received");
				return req.CreateResponse<object>(new
				{
					validationResponse = ((dynamic)messages[0].Data)["validationCode"]
				});
			}

			try
			{
				var storageClient = Locator.CreateStorageManagementClient(await Locator.GetTokenCredentialsAsync());

				foreach (var message in messages)
				{
					string sourceUrl = ((dynamic)messages[0].Data)["url"];
					var sourceUri = new Uri(sourceUrl);

					var sourceCredentials = await Locator.GetStorageKeyCredentialsAsync(sourceUri, storageClient);
					var sourceBlob = new CloudBlockBlob(sourceUri, sourceCredentials);

					var sourceSasPolicy = new SharedAccessBlobPolicy
					{
						Permissions = SharedAccessBlobPermissions.Read,
						SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
						SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1)
					};

					foreach (var destinationRegion in Locator.DestinationRegions)
					{
						var destContainerUri = GetDestinationUri(sourceBlob.Container.Uri, destinationRegion);
						var destBlobUri = GetDestinationUri(sourceUri, destinationRegion);
						var destCredentials = await Locator.GetStorageKeyCredentialsAsync(destBlobUri, storageClient);

						var destContainer = new CloudBlobContainer(destContainerUri, destCredentials);
						await destContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, new BlobRequestOptions(), new OperationContext());

						var destBlob = new CloudBlockBlob(destBlobUri, destCredentials);

						var sas = sourceBlob.GetSharedAccessSignature(sourceSasPolicy);
						var sourceSasUrl = $"{sourceUri.OriginalString}{sas}";

						await destBlob.StartCopyAsync(new Uri(sourceSasUrl));
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.Write(ex.StackTrace);
				log.LogError(ex, ex.Message);

				return req.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
			}

			return req.CreateResponse(HttpStatusCode.OK);
		}

		private static Uri GetDestinationUri(Uri sourceUri, string destinationRegion)
		{
			return new Uri(sourceUri.OriginalString.Replace(Locator.Region, destinationRegion));
		}

	}
}
