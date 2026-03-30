using Azure.Storage.Blobs;
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

        var connectionString = configuration["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");

        var containerName = configuration["BlobStorage:ContainerName"] ?? "loan-documents";

        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string blobPath,
        string contentType,
        CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = _container.GetBlobClient(blobPath);

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

        // Check if we can generate SAS (requires SharedKey credentials — not available with connection string "UseDevelopmentStorage=true" in all SDKs)
        if (blobClient.CanGenerateSasUri)
        {
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

        // Fallback for Azurite local development — return direct URI
        // Azurite does not require SAS for local dev
        _logger.LogWarning(
            "Cannot generate SAS URI for blob '{BlobPath}'. Returning direct URI (local development only).",
            blobPath);

            return await Task.FromResult(blobClient.Uri.ToString());
    }
}
