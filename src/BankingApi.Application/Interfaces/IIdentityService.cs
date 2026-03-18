namespace BankingApi.Application.Interfaces;

public interface IIdentityService
{
    Task<string?> GetDisplayNameAsync(string userId);
}
