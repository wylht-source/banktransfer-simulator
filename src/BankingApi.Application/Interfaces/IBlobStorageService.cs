namespace BankingApi.Application.Interfaces;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a file stream to blob storage.
    /// Returns the blob path on success.
    /// </summary>
    Task<string> UploadAsync(
        Stream fileStream,
        string blobPath,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a temporary SAS URI for secure, time-limited download.
    /// The URI is not stored — generated on demand.
    /// </summary>
    Task<string> GenerateDownloadUriAsync(
        string blobPath,
        TimeSpan expiry,
        CancellationToken ct = default);
}
