using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Accounts.Queries;
using BankingApi.Application.Transactions.Queries;
using BankingApi.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankingApi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly CreateAccountHandler _createHandler;
    private readonly GetAccountHandler _getHandler;
    private readonly GetStatementHandler _statementHandler;

    public AccountsController(
        CreateAccountHandler createHandler,
        GetAccountHandler getHandler,
        GetStatementHandler statementHandler)
    {
        _createHandler = createHandler;
        _getHandler = getHandler;
        _statementHandler = statementHandler;
    }

    /// <summary>Creates a new bank account for the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAccountResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        try
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")!;

            var result = await _createHandler.HandleAsync(
                new CreateAccountCommand(request.OwnerName, ownerId), ct);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns account details and current balance.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetAccountResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")!;

            var result = await _getHandler.HandleAsync(new GetAccountQuery(id, ownerId), ct);
            return Ok(result);
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found")
                ? NotFound(new { error = ex.Message })
                : StatusCode(403, new { error = ex.Message });
        }
    }

    /// <summary>Returns paginated transaction history for an account.</summary>
    [HttpGet("{id:guid}/statement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatement(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")!;

            var result = await _statementHandler.HandleAsync(
                new GetStatementQuery(id, ownerId, page, pageSize), ct);

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

public record CreateAccountRequest(string OwnerName);