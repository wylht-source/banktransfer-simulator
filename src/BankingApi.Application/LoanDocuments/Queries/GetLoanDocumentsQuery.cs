using BankingApi.Application.Interfaces;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Exceptions;

namespace BankingApi.Application.LoanDocuments.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record LoanDocumentDto(
    Guid     DocumentId,
    Guid     LoanId,
    string   OriginalFileName,
    string   ContentType,
    long     SizeBytes,
    string?  DocumentType,
    string   StorageProvider,
    DateTime UploadedAt,
    string   UploadedByUserId);

// ── Get Documents Query ───────────────────────────────────────────────────────

public record GetLoanDocumentsQuery(
    Guid   LoanId,
    string RequesterId,
    string RequesterRole);

public class GetLoanDocumentsHandler(
    ILoanRepository loanRepository,
    ILoanDocumentRepository documentRepository)
{
    private static readonly string[] ApproverRoles =
    [
        "Manager", "Supervisor", "CreditCommittee"
    ];

    public async Task<IEnumerable<LoanDocumentDto>> Handle(
        GetLoanDocumentsQuery query, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(query.LoanId, ct)
            ?? throw new DomainException($"Loan '{query.LoanId}' not found.");

        var isApprover = ApproverRoles.Contains(query.RequesterRole);
        if (!isApprover && loan.ClientId != query.RequesterId)
            throw new DomainException("Access denied.");

        var documents = await documentRepository.GetByLoanIdAsync(query.LoanId, ct);

        return documents.Select(d => new LoanDocumentDto(
            DocumentId:      d.Id,
            LoanId:          d.LoanId,
            OriginalFileName: d.OriginalFileName,
            ContentType:     d.ContentType,
            SizeBytes:       d.SizeBytes,
            DocumentType:    d.DocumentType,
            StorageProvider: d.StorageProvider,
            UploadedAt:      d.UploadedAt,
            UploadedByUserId: d.UploadedByUserId));
    }
}

// ── Get Download URI Query ────────────────────────────────────────────────────

public record GetDocumentDownloadUriQuery(
    Guid   LoanId,
    Guid   DocumentId,
    string RequesterId,
    string RequesterRole);

public record GetDocumentDownloadUriResult(
    Guid     DocumentId,
    string   OriginalFileName,
    string   DownloadUri,
    DateTime ExpiresAt);

public class GetDocumentDownloadUriHandler(
    ILoanRepository loanRepository,
    ILoanDocumentRepository documentRepository,
    IBlobStorageService blobStorageService)
{
    private static readonly string[] ApproverRoles =
    [
        "Manager", "Supervisor", "CreditCommittee"
    ];

    private static readonly TimeSpan SasExpiry = TimeSpan.FromMinutes(15);

    public async Task<GetDocumentDownloadUriResult> Handle(
        GetDocumentDownloadUriQuery query, CancellationToken ct = default)
    {
        var loan = await loanRepository.GetByIdAsync(query.LoanId, ct)
            ?? throw new DomainException($"Loan '{query.LoanId}' not found.");

        var isApprover = ApproverRoles.Contains(query.RequesterRole);
        if (!isApprover && loan.ClientId != query.RequesterId)
            throw new DomainException("Access denied.");

        var document = await documentRepository.GetByIdAsync(query.DocumentId, ct)
            ?? throw new DomainException($"Document '{query.DocumentId}' not found.");

        if (document.LoanId != query.LoanId)
            throw new DomainException("Document does not belong to this loan.");

        var downloadUri = await blobStorageService.GenerateDownloadUriAsync(
            document.BlobPath, SasExpiry, ct);

        return new GetDocumentDownloadUriResult(
            DocumentId:      document.Id,
            OriginalFileName: document.OriginalFileName,
            DownloadUri:     downloadUri,
            ExpiresAt:       DateTime.UtcNow.Add(SasExpiry));
    }
}
