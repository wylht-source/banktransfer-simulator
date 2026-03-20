using BankingApi.Application.Loans.Commands;
using BankingApi.Application.Loans.Queries;
using BankingApi.Domain.Entities;
using BankingApi.Domain.Enums;
using BankingApi.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankingApi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly RequestLoanHandler _requestHandler;
    private readonly RequestPayrollLoanHandler _requestPayrollHandler;
    private readonly ApproveLoanHandler _approveHandler;
    private readonly RejectLoanHandler _rejectHandler;
    private readonly CancelLoanHandler _cancelHandler;
    private readonly GetLoanHandler _getLoanHandler;
    private readonly GetMyLoansHandler _getMyLoansHandler;
    private readonly GetPendingLoansHandler _getPendingHandler;
    private readonly GetDecidedLoansHandler _getDecidedHandler;
    private readonly GetLoanApprovalDetailsHandler _getApprovalDetailsHandler;

    public LoansController(
        RequestLoanHandler requestHandler,
        RequestPayrollLoanHandler requestPayrollHandler,
        ApproveLoanHandler approveHandler,
        RejectLoanHandler rejectHandler,
        CancelLoanHandler cancelHandler,
        GetLoanHandler getLoanHandler,
        GetMyLoansHandler getMyLoansHandler,
        GetPendingLoansHandler getPendingHandler,
        GetDecidedLoansHandler getDecidedHandler,
        GetLoanApprovalDetailsHandler getApprovalDetailsHandler)
    {
        _requestHandler            = requestHandler;
        _requestPayrollHandler     = requestPayrollHandler;
        _approveHandler            = approveHandler;
        _rejectHandler             = rejectHandler;
        _cancelHandler             = cancelHandler;
        _getLoanHandler            = getLoanHandler;
        _getMyLoansHandler         = getMyLoansHandler;
        _getPendingHandler         = getPendingHandler;
        _getDecidedHandler         = getDecidedHandler;
        _getApprovalDetailsHandler = getApprovalDetailsHandler;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!;

    private string HighestRole => GetHighestRole(User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value));

    private static string GetHighestRole(IEnumerable<string> roles)
    {
        if (roles.Contains(Loan.RoleCreditCommittee)) return Loan.RoleCreditCommittee;
        if (roles.Contains(Loan.RoleSupervisor))      return Loan.RoleSupervisor;
        if (roles.Contains(Loan.RoleManager))         return Loan.RoleManager;
        return roles.FirstOrDefault() ?? "Client";
    }

    // ── Client endpoints ──────────────────────────────────────────────────────

    /// <summary>Client requests a new personal loan.</summary>
    [HttpPost("request")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(RequestLoanResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestLoan([FromBody] RequestLoanRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _requestHandler.Handle(
                new RequestLoanCommand(UserId, request.Amount, request.Installments), ct);

            return CreatedAtAction(nameof(GetById), new { id = result.LoanId }, result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Client requests a new payroll loan.</summary>
    [HttpPost("request-payroll")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(RequestPayrollLoanResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPayroll([FromBody] RequestPayrollLoanRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _requestPayrollHandler.Handle(
                new RequestPayrollLoanCommand(
                    UserId, request.Amount, request.Installments,
                    request.EmployerName, request.MonthlySalary,
                    request.EmploymentStatus, request.ExistingPayrollDeductions), ct);

            return CreatedAtAction(nameof(GetById), new { id = result.LoanId }, result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns paginated list of the authenticated client's loans.</summary>
    [HttpGet("my-loans")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyLoans(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _getMyLoansHandler.Handle(
            new GetMyLoansQuery(UserId, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>Client cancels their own pending loan.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Client")]
    [ProducesResponseType(typeof(CancelLoanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _cancelHandler.Handle(new CancelLoanCommand(id, UserId), ct);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    // ── Shared endpoints ──────────────────────────────────────────────────────

    /// <summary>Returns loan details including approval history.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LoanDetailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _getLoanHandler.Handle(
                new GetLoanQuery(id, UserId, HighestRole), ct);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : StatusCode(403, new { error = ex.Message });
        }
    }

    // ── Approver endpoints ────────────────────────────────────────────────────

    /// <summary>Returns pending loans the authenticated approver has authority to action.</summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Manager,Supervisor,CreditCommittee")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _getPendingHandler.Handle(
            new GetPendingLoansQuery(HighestRole, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>Returns approved and rejected loans within the approver's authority.</summary>
    [HttpGet("decided")]
    [Authorize(Roles = "Manager,Supervisor,CreditCommittee")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDecided(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _getDecidedHandler.Handle(
            new GetDecidedLoansQuery(HighestRole, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>Returns full approval details including payment schedule and bank profitability view.</summary>
    [HttpGet("{id:guid}/approval-details")]
    [Authorize(Roles = "Manager,Supervisor,CreditCommittee")]
    [ProducesResponseType(typeof(LoanApprovalDetailsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApprovalDetails(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _getApprovalDetailsHandler.Handle(
                new GetLoanApprovalDetailsQuery(id, UserId, HighestRole), ct);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : StatusCode(403, new { error = ex.Message });
        }
    }

    /// <summary>Approves a pending loan. Requires sufficient role authority.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Manager,Supervisor,CreditCommittee")]
    [ProducesResponseType(typeof(ApproveLoanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _approveHandler.Handle(
                new ApproveLoanCommand(id, UserId, HighestRole), ct);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Rejects a pending loan. Reason is mandatory.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager,Supervisor,CreditCommittee")]
    [ProducesResponseType(typeof(RejectLoanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectLoanRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _rejectHandler.Handle(
                new RejectLoanCommand(id, UserId, HighestRole, request.Reason), ct);

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : BadRequest(new { error = ex.Message });
        }
    }
}

public record RequestLoanRequest(decimal Amount, int Installments);
public record RejectLoanRequest(string Reason);

public record RequestPayrollLoanRequest(
    decimal Amount,
    int Installments,
    string EmployerName,
    decimal MonthlySalary,
    EmploymentStatus EmploymentStatus,
    decimal ExistingPayrollDeductions);