using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace BankingApi.Application.LoanDocuments.Commands;

// ── Allowed file config ───────────────────────────────────────────────────────
public static class DocumentUploadPolicy
{
    public static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png"
    ];

    public static readonly string[] AllowedExtensions =
    [
        ".pdf", ".jpg", ".jpeg", ".png"
    ];

    public const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB
}

// ── Command ──────────────────────────────────────────────────────────────────
public record UploadLoanDocumentCommand(
    Guid LoanId,
    string UploadedByUserId,
    string UploadedByRole,
    Stream FileStream,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? DocumentType);

// ── Result ───────────────────────────────────────────────────────────────────
public record UploadLoanDocumentResult(
    Guid DocumentId,
    Guid LoanId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? DocumentType,
    string StorageProvider,
    DateTime UploadedAt);

// ── Handler ──────────────────────────────────────────────────────────────────
public class UploadLoanDocumentHandler(
    ILoanRepository loanRepository,
    ILoanDocumentRepository documentRepository,
    IBlobStorageService blobStorageService,
    ILogger<UploadLoanDocumentHandler> logger)
{
    private static readonly string[] ApproverRoles =
    [
        "Manager", "Supervisor", "CreditCommittee"
    ];

    public async Task<UploadLoanDocumentResult> Handle(
        UploadLoanDocumentCommand command, CancellationToken ct = default)
    {
        // Validate loan exists and requester has access
        var loan = await loanRepository.GetByIdAsync(command.LoanId, ct)
            ?? throw new DomainException($"Loan '{command.LoanId}' not found.");

        var isApprover = ApproverRoles.Contains(command.UploadedByRole);
        if (!isApprover && loan.ClientId != command.UploadedByUserId)
            throw new DomainException("Access denied.");

        // Validate file
        ValidateFile(command.OriginalFileName, command.ContentType, command.SizeBytes);

        // Compute SHA256
        var sha256 = await ComputeSha256Async(command.FileStream, ct);
        command.FileStream.Position = 0; // reset after hashing

        // Build blob path: loans/{loanId}/documents/{documentId}
        var documentId = Guid.NewGuid();
        var blobPath = $"loans/{command.LoanId}/documents/{documentId}";

        // Upload to blob storage
        await blobStorageService.UploadAsync(
            command.FileStream, blobPath, command.ContentType, ct);

        // Persist metadata
        var document = new LoanDocument(
            loanId: command.LoanId,
            uploadedByUserId: command.UploadedByUserId,
            blobPath: blobPath,
            originalFileName: command.OriginalFileName,
            contentType: command.ContentType,
            sizeBytes: command.SizeBytes,
            documentType: command.DocumentType,
            sha256: sha256);

        // Set Id to match the one used in blobPath
        await documentRepository.AddAsync(document, ct);
        await documentRepository.SaveChangesAsync(ct);
        logger.LogInformation(
    "LoanDocumentUploaded — DocumentId: {DocumentId}, LoanId: {LoanId}, FileName: {FileName}, ContentType: {ContentType}, SizeBytes: {SizeBytes}, UploadedBy: {UserId}",
    document.Id, document.LoanId, document.OriginalFileName,
    document.ContentType, document.SizeBytes, document.UploadedByUserId);

        return new UploadLoanDocumentResult(
            DocumentId: document.Id,
            LoanId: document.LoanId,
            OriginalFileName: document.OriginalFileName,
            ContentType: document.ContentType,
            SizeBytes: document.SizeBytes,
            DocumentType: document.DocumentType,
            StorageProvider: document.StorageProvider,
            UploadedAt: document.UploadedAt);
    }

    private static void ValidateFile(string fileName, string contentType, long sizeBytes)
    {
        if (sizeBytes > DocumentUploadPolicy.MaxSizeBytes)
            throw new DomainException($"File size exceeds the maximum allowed size of 10 MB.");

        if (!DocumentUploadPolicy.AllowedContentTypes.Contains(contentType.ToLower()))
            throw new DomainException($"File type '{contentType}' is not allowed. Accepted: PDF, JPG, PNG.");

        var extension = Path.GetExtension(fileName).ToLower();
        if (!DocumentUploadPolicy.AllowedExtensions.Contains(extension))
            throw new DomainException($"File extension '{extension}' is not allowed. Accepted: .pdf, .jpg, .jpeg, .png.");
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
