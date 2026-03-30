using BankingApi.Application.LoanDocuments.Commands;
using BankingApi.Application.LoanDocuments.Queries;
using BankingApi.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankingApi.API.Controllers;

[ApiController]
[Route("api/loans/{loanId:guid}/documents")]
[Authorize]
public class LoanDocumentsController : ControllerBase
{
    private readonly UploadLoanDocumentHandler _uploadHandler;
    private readonly GetLoanDocumentsHandler _getDocumentsHandler;
    private readonly GetDocumentDownloadUriHandler _getDownloadUriHandler;

    public LoanDocumentsController(
        UploadLoanDocumentHandler uploadHandler,
        GetLoanDocumentsHandler getDocumentsHandler,
        GetDocumentDownloadUriHandler getDownloadUriHandler)
    {
        _uploadHandler        = uploadHandler;
        _getDocumentsHandler  = getDocumentsHandler;
        _getDownloadUriHandler = getDownloadUriHandler;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!;

    private string HighestRole => GetHighestRole(User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value));

    private static string GetHighestRole(IEnumerable<string> roles)
    {
        if (roles.Contains("CreditCommittee")) return "CreditCommittee";
        if (roles.Contains("Supervisor"))      return "Supervisor";
        if (roles.Contains("Manager"))         return "Manager";
        return roles.FirstOrDefault() ?? "Client";
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

    /// <summary>Upload a document for a loan. Accepted: PDF, JPG, PNG — max 10 MB.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UploadLoanDocumentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upload(
        Guid loanId,
        IFormFile file,
        [FromForm] string? documentType,
        CancellationToken ct)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file was uploaded." });

            await using var stream = file.OpenReadStream();

            var result = await _uploadHandler.Handle(new UploadLoanDocumentCommand(
                LoanId:           loanId,
                UploadedByUserId: UserId,
                UploadedByRole:   HighestRole,
                FileStream:       stream,
                OriginalFileName: file.FileName,
                ContentType:      file.ContentType,
                SizeBytes:        file.Length,
                DocumentType:     documentType), ct);

            return CreatedAtAction(nameof(GetDocuments), new { loanId }, result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>List all documents for a loan.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LoanDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocuments(Guid loanId, CancellationToken ct)
    {
        try
        {
            var result = await _getDocumentsHandler.Handle(
                new GetLoanDocumentsQuery(loanId, UserId, HighestRole), ct);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : StatusCode(403, new { error = ex.Message });
        }
    }

    /// <summary>Generate a temporary (15 min) secure download URI for a document.</summary>
    [HttpGet("{documentId:guid}/download")]
    [ProducesResponseType(typeof(GetDocumentDownloadUriResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadUri(
        Guid loanId, Guid documentId, CancellationToken ct)
    {
        try
        {
            var result = await _getDownloadUriHandler.Handle(
                new GetDocumentDownloadUriQuery(loanId, documentId, UserId, HighestRole), ct);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : StatusCode(403, new { error = ex.Message });
        }
    }
}
