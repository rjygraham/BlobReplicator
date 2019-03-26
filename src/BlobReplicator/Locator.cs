using Microsoft.Azure.Management.EventGrid;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BlobReplicator
{
	internal static class Locator
	{
		internal static string SubscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID") ?? "f744c2ea-2976-41d9-9719-ff08dd26b5bd";
		internal static string ResourceGroup = Environment.GetEnvironmentVariable("RESOURCE_GROUP") ?? "mrr-test";
		internal static string Region = Environment.GetEnvironmentVariable("REGION") ?? "eastus";
		internal static string[] DestinationRegions = Environment.GetEnvironmentVariable("DESTINATION_REGIONS")?.Split(',') ?? "westus2".Split(',');
		internal static string Hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? "localhost:7071";

		internal static async Task<TokenCredentials> GetTokenCredentialsAsync()
		{
			var azureServiceTokenProvider = new AzureServiceTokenProvider();
			return new TokenCredentials(await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/"));
		}

		internal static async Task<TokenCredential> GetStroageTokenCredentialAsync()
		{
			var azureServiceTokenProvider = new AzureServiceTokenProvider();
			return new TokenCredential(await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/"));
		}

		internal static ResourceManagementClient CreateResourceManagementClient(TokenCredentials credentials)
		{
			var client = new ResourceManagementClient(credentials);
			client.SubscriptionId = SubscriptionId;

			return client;
		}

		internal static async Task<StorageCredentials> GetStorageKeyCredentialsAsync(Uri blobUri, StorageManagementClient storageClient)
		{
			var storageAccountName = blobUri.Authority.Split('.')[0];
			var listKeysResult = await storageClient.StorageAccounts.ListKeysAsync(ResourceGroup, storageAccountName);
			return new StorageCredentials(storageAccountName, listKeysResult.Keys[0].Value);
		}

		internal static StorageManagementClient CreateStorageManagementClient(TokenCredentials credentials)
		{
			var client = new StorageManagementClient(credentials);
			client.SubscriptionId = SubscriptionId;

			return client;
		}

		internal static EventGridManagementClient CreateEventGridManagementClient(TokenCredentials credentials)
		{
			var client = new EventGridManagementClient(credentials);
			client.SubscriptionId = SubscriptionId;

			return client;
		}
	}
}
