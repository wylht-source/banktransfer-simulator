using BankingApi.Application.Transactions.Commands;
using BankingApi.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly DepositHandler _depositHandler;
    private readonly WithdrawHandler _withdrawHandler;
    private readonly TransferHandler _transferHandler;

    public TransactionsController(
        DepositHandler depositHandler,
        WithdrawHandler withdrawHandler,
        TransferHandler transferHandler)
    {
        _depositHandler = depositHandler;
        _withdrawHandler = withdrawHandler;
        _transferHandler = transferHandler;
    }

    /// <summary>
    /// Deposit funds into an account.
    /// Requires Idempotency-Key header to prevent duplicate processing.
    /// </summary>
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(DepositResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deposit(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        [FromBody] DepositRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _depositHandler.HandleAsync(
                new DepositCommand(request.AccountId, request.Amount, request.Description, idempotencyKey), ct);

            if (result.WasDuplicate)
                Response.Headers.Append("X-Idempotency-Replayed", "true");

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Withdraw funds from an account.
    /// Requires Idempotency-Key header to prevent duplicate processing.
    /// </summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(WithdrawResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        [FromBody] WithdrawRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _withdrawHandler.HandleAsync(
                new WithdrawCommand(request.AccountId, request.Amount, request.Description, idempotencyKey), ct);

            if (result.WasDuplicate)
                Response.Headers.Append("X-Idempotency-Replayed", "true");

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Transfer funds between two accounts atomically.
    /// Requires Idempotency-Key header to prevent duplicate processing.
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Transfer(
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        [FromBody] TransferRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _transferHandler.HandleAsync(
                new TransferCommand(request.FromAccountId, request.ToAccountId, request.Amount, request.Description, idempotencyKey), ct);

            if (result.WasDuplicate)
                Response.Headers.Append("X-Idempotency-Replayed", "true");

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record DepositRequest(Guid AccountId, decimal Amount, string Description);
public record WithdrawRequest(Guid AccountId, decimal Amount, string Description);
public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string Description);