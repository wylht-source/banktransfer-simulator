using BankingApi.Application.Accounts.Commands;
using BankingApi.Application.Transactions.Commands;
using BankingApi.Application.Transactions.Queries;
using BankingApi.Domain.Exceptions;
using BankingApi.Infrastructure.Repositories;
using FluentAssertions;

namespace BankingApi.Tests.Integration;

public class TransactionHandlerTests
{
    private readonly BankingApi.Infrastructure.Persistence.BankingDbContext _db;
    private readonly AccountRepository _accountRepo;
    private readonly TransactionRepository _transactionRepo;
    private readonly CreateAccountHandler _createAccountHandler;
    private readonly DepositHandler _depositHandler;
    private readonly WithdrawHandler _withdrawHandler;
    private readonly TransferHandler _transferHandler;
    private readonly GetStatementHandler _statementHandler;

    private const string UserId1 = "user-integration-1";
    private const string UserId2 = "user-integration-2";

    public TransactionHandlerTests()
    {
        _db = BankingDbContextFactory.Create();

        // Use a logger-less version of AccountRepository for tests
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountRepository>.Instance;
        _accountRepo = new AccountRepository(_db, logger);
        _transactionRepo = new TransactionRepository(_db);

        _createAccountHandler = new CreateAccountHandler(_accountRepo);
        _depositHandler = new DepositHandler(_accountRepo, _transactionRepo);
        _withdrawHandler = new WithdrawHandler(_accountRepo, _transactionRepo);
        _transferHandler = new TransferHandler(_accountRepo, _transactionRepo);
        _statementHandler = new GetStatementHandler(_accountRepo, _transactionRepo);
    }

    [Fact]
    public async Task Deposit_ValidAmount_UpdatesBalance()
    {
        var account = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));

        var result = await _depositHandler.HandleAsync(
            new DepositCommand(account.Id, 500m, "Salary", Guid.NewGuid(), UserId1));

        result.NewBalance.Should().Be(500m);
        result.WasDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task Deposit_DuplicateIdempotencyKey_ReturnsDuplicate()
    {
        var account = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));
        var key = Guid.NewGuid();

        await _depositHandler.HandleAsync(new DepositCommand(account.Id, 500m, "First", key, UserId1));
        var result = await _depositHandler.HandleAsync(new DepositCommand(account.Id, 500m, "Second", key, UserId1));

        result.WasDuplicate.Should().BeTrue();

        // Balance should still be 500, not 1000
        var dbAccount = await _accountRepo.GetByIdAsync(account.Id);
        dbAccount!.Balance.Should().Be(500m);
    }

    [Fact]
    public async Task Withdraw_InsufficientFunds_ThrowsDomainException()
    {
        var account = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));
        await _depositHandler.HandleAsync(new DepositCommand(account.Id, 100m, "Setup", Guid.NewGuid(), UserId1));

        var act = async () => await _withdrawHandler.HandleAsync(
            new WithdrawCommand(account.Id, 500m, "Overdraft", Guid.NewGuid(), UserId1));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public async Task Transfer_ValidAmount_UpdatesBothBalances()
    {
        var from = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));
        var to = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Tanaka", UserId2));

        await _depositHandler.HandleAsync(new DepositCommand(from.Id, 1000m, "Setup", Guid.NewGuid(), UserId1));

        await _transferHandler.HandleAsync(
            new TransferCommand(from.Id, to.Id, 400m, "Rent", Guid.NewGuid(), UserId1));

        var fromAccount = await _accountRepo.GetByIdAsync(from.Id);
        var toAccount = await _accountRepo.GetByIdAsync(to.Id);

        fromAccount!.Balance.Should().Be(600m);
        toAccount!.Balance.Should().Be(400m);
    }

    [Fact]
    public async Task Transfer_WrongOwner_ThrowsDomainException()
    {
        var from = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));
        var to = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Tanaka", UserId2));
        await _depositHandler.HandleAsync(new DepositCommand(from.Id, 1000m, "Setup", Guid.NewGuid(), UserId1));

        // UserId2 tries to transfer from UserId1's account
        var act = async () => await _transferHandler.HandleAsync(
            new TransferCommand(from.Id, to.Id, 400m, "Unauthorized", Guid.NewGuid(), UserId2));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Access denied*");
    }

    [Fact]
    public async Task GetStatement_ReturnsPagedTransactions()
    {
        var account = await _createAccountHandler.HandleAsync(new CreateAccountCommand("Sergio", UserId1));

        await _depositHandler.HandleAsync(new DepositCommand(account.Id, 100m, "Dep 1", Guid.NewGuid(), UserId1));
        await _depositHandler.HandleAsync(new DepositCommand(account.Id, 200m, "Dep 2", Guid.NewGuid(), UserId1));
        await _depositHandler.HandleAsync(new DepositCommand(account.Id, 300m, "Dep 3", Guid.NewGuid(), UserId1));

        var result = await _statementHandler.HandleAsync(
            new GetStatementQuery(account.Id, UserId1, Page: 1, PageSize: 2));

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.HasNext.Should().BeTrue();
    }
}
