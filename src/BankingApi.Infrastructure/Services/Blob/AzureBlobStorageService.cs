using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using BankingApi.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankingApi.Infrastructure.Services.Blob;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        IConfiguration configuration,
        ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;

        var containerName      = configuration["BlobStorage:ContainerName"] ?? "loan-documents";
        var connectionString   = configuration["BlobStorage:ConnectionString"];
        var storageAccountUri  = configuration["BlobStorage:AccountUri"];

        if (!string.IsNullOrWhiteSpace(storageAccountUri))
        {
            // Production — use Managed Identity (System-Assigned)
            // No secrets needed — DefaultAzureCredential resolves via Managed Identity on Azure
            var serviceClient = new BlobServiceClient(
                new Uri(storageAccountUri),
                new DefaultAzureCredential());

            _container = serviceClient.GetBlobContainerClient(containerName);

            _logger.LogInformation(
                "BlobStorage: using Managed Identity for account '{Uri}'", storageAccountUri);
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Local development — use connection string (Azurite)
            _container = new BlobContainerClient(connectionString, containerName);

            _logger.LogInformation(
                "BlobStorage: using connection string (local/Azurite)");
        }
        else
        {
            throw new InvalidOperationException(
                "BlobStorage is not configured. Set either 'BlobStorage:AccountUri' (production) " +
                "or 'BlobStorage:ConnectionString' (local development).");
        }
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string blobPath,
        string contentType,
        CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient    = _container.GetBlobClient(blobPath);
        var uploadOptions = new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        await blobClient.UploadAsync(fileStream, uploadOptions, ct);

        _logger.LogInformation("Blob uploaded: {BlobPath}", blobPath);

        return blobPath;
    }

    public async Task<string> GenerateDownloadUriAsync(
        string blobPath,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        if (blobClient.CanGenerateSasUri)
        {
            // Works with SharedKey credentials (connection string)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName          = blobPath,
                Resource          = "b",
                ExpiresOn         = DateTimeOffset.UtcNow.Add(expiry)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            return blobClient.GenerateSasUri(sasBuilder).ToString();
        }

        // Managed Identity — use User Delegation SAS
        // Requires Storage Blob Data Contributor role on the Managed Identity
        var serviceClient     = _container.GetParentBlobServiceClient();
        var delegationKey     = await serviceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.Add(expiry),
            ct);

        var udSasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName          = blobPath,
            Resource          = "b",
            ExpiresOn         = DateTimeOffset.UtcNow.Add(expiry)
        };
        udSasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(udSasBuilder);
        return sasUri.ToString();
    }
}