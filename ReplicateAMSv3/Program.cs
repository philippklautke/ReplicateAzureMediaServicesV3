﻿using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using System;
using System.Threading.Tasks;
using ReplicateAMSv3.Managers;

namespace ReplicateAMSv3
{
    class Program
    {
        private static AppSettings _appSettings;
        private static IAzureMediaServicesClient _sourceClient;
        private static IAzureMediaServicesClient _destinationClient;

        static void Main(string[] args)
        {
            AppDomain appDomain = AppDomain.CurrentDomain;
            appDomain.UnhandledException += AppDomain_UnhandledException;

            Helpers.CreateLogFile();

            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Start replicating AMS account:-", 1);

            CacheSettings();
            CacheAmsClients().Wait();

            Helpers.WriteLine(FormatMoveMessage("Subscription ID", _appSettings.Source.SubscriptionId, _appSettings.Destination.SubscriptionId), 2);

            Helpers.WriteLine(FormatMoveMessage("AMS Account", _appSettings.Source.AccountName, _appSettings.Destination.AccountName), 2);

            Helpers.WriteLine(FormatMoveMessage("Resource Group", _appSettings.Source.ResourceGroup, _appSettings.Destination.ResourceGroup), 2);

            Helpers.WriteLine(FormatMoveMessage("Storage Account", _appSettings.Source.StorageAccountName, _appSettings.Destination.StorageAccountName), 2);

            Helpers.WriteLine(FormatMoveMessage("Location", _appSettings.Source.Location, _appSettings.Destination.Location), 2);

            Helpers.WriteLine("Source AAD Settings: " + _appSettings.Source.AADSettings, 2);

            Helpers.WriteLine("Destination AAD Settings: " + _appSettings.Destination.AADSettings, 2);

            Helpers.WriteLine("Copy Using Local Network: " + _appSettings.Miscellaneous.CopyUsingLocalNetwork, 2);

            ReplicateAccountFilters();

            ReplicateContentKeyPolicies();

            ReplicateTransforms();

            ReplicateStreamingEndpoints();

            ReplicateAssets();

            ReplicateStreamingLocators();

            ReplicateLiveEvents();

            Helpers.WriteLine($"{Environment.NewLine}[{DateTime.Now:dd/mm/yyyy HH:mm}] Replication done successfully!", 1);
            Console.ReadLine();
        }

        private static string FormatMoveMessage(string message, string source, string destination)
        {
            return $"{message}: {source} --> {destination}";
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Helpers.WriteLine(Environment.NewLine + Environment.NewLine + "   ***** Exception occurred! *****", 1);

            Exception ex = e.ExceptionObject as Exception;

            if (ex is ApiErrorException)
            {
                ApiErrorException apiEx = ex as ApiErrorException;
                if (apiEx.Body != null && apiEx.Body.Error != null)
                {
                    Helpers.WriteLine($"API error code '{apiEx.Body.Error.Code}' and message '{apiEx.Body.Error.Message}' {Environment.NewLine}", 1);
                }
            }

            Helpers.WriteLine($"Message: {ex.Message}", 1);
            Helpers.WriteLine($"Stack trace: {ex.StackTrace}", 1);
        }

        private static void CacheSettings()
        {
            IConfiguration configuration = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", true, true)
               .Build();

            IConfigurationSection configSection = configuration.GetSection("appConfig");

            _appSettings = new AppSettings(configSection.GetSection("sourceConfig").Get<ServicePrincipalAuth>(), configSection.GetSection("destinationConfig").Get<ServicePrincipalAuth>(), configSection.GetSection("miscellaneous").Get<Miscellaneous>());
        }

        private static async Task CacheAmsClients()
        {
            _sourceClient = await InitializeAmsClient(_appSettings.Source);
            _destinationClient = await InitializeAmsClient(_appSettings.Destination);
        }

        private static async Task<IAzureMediaServicesClient> InitializeAmsClient(ServicePrincipalAuth spAuth)
        {
            ClientCredential clientCredential = new ClientCredential(spAuth.AadClientId, spAuth.AadSecret);

            ServiceClientCredentials serviceCredential = await ApplicationTokenProvider.LoginSilentAsync(spAuth.AadTenantId, clientCredential, Helpers.GetActiveDirectoryServiceSettings(spAuth.AADSettings));

            return new AzureMediaServicesClient(spAuth.ArmEndpoint, serviceCredential)
            {
                SubscriptionId = spAuth.SubscriptionId
            };
        }

        private static bool ReplicateAccountFilters()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 1 of 7: Replicate account filters", 1);
            AccountFilterManager accountFilterManager = new AccountFilterManager();
            accountFilterManager.Initialize(_sourceClient.AccountFilters, _destinationClient.AccountFilters, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous);
            return accountFilterManager.Replicate();
        }

        private static bool ReplicateContentKeyPolicies()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 2 of 7: Replicate content key policies", 1);
            ContentKeyPolicyManager contentKeyPolicyManager = new ContentKeyPolicyManager();
            contentKeyPolicyManager.Initialize(_sourceClient.ContentKeyPolicies, _destinationClient.ContentKeyPolicies, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous);
            return contentKeyPolicyManager.Replicate();
        }

        private static bool ReplicateTransforms()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 3 of 7: Replicate transforms", 1);
            TransformManager transformManager = new TransformManager();
            transformManager.Initialize(_sourceClient.Transforms, _destinationClient.Transforms, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous);
            return transformManager.Replicate();
        }

        private static bool ReplicateStreamingEndpoints()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 4 of 7: Replicate streaming endpoints", 1);
            StreamingEndpointManager streamingEndpointManager = new StreamingEndpointManager();
            streamingEndpointManager.Initialize(_sourceClient.StreamingEndpoints, _destinationClient.StreamingEndpoints, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous);
            return streamingEndpointManager.Replicate();
        }

        private static bool ReplicateAssets()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 5 of 7: Replicate assets", 1);
            AssetManager assetManager = new AssetManager();
            assetManager.Initialize(_sourceClient.Assets, _destinationClient.Assets, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous, _sourceClient.AssetFilters, _destinationClient.AssetFilters);
            return assetManager.Replicate();
        }

        private static bool ReplicateLiveEvents()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 7 of 7: Replicate live events", 1);
            LiveEventsManager liveEventsManager = new LiveEventsManager();
            liveEventsManager.Initialize(_sourceClient.LiveEvents, _destinationClient.LiveEvents, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous, _sourceClient.LiveOutputs, _destinationClient.LiveOutputs);
            return liveEventsManager.Replicate();
        }

        private static bool ReplicateStreamingLocators()
        {
            Helpers.WriteLine(Environment.NewLine, 1);
            Helpers.WriteLine($"[{DateTime.Now:dd/mm/yyyy HH:mm}] Step 6 of 7: Replicate streaming locators", 1);
            StreamingLocatorsManager streamingLocatorsManager = new StreamingLocatorsManager();
            streamingLocatorsManager.Initialize(_sourceClient.StreamingLocators, _destinationClient.StreamingLocators, _appSettings.Source, _appSettings.Destination, _appSettings.Miscellaneous);
            return streamingLocatorsManager.Replicate();
        }
    }
}
