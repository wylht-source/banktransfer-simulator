using BankingApi.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace BankingApi.Infrastructure.Services;

public class IdentityService(UserManager<IdentityUser> userManager) : IIdentityService
{
    public async Task<string?> GetDisplayNameAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;
        return !string.IsNullOrWhiteSpace(user.UserName) ? user.UserName : user.Email;
    }
}
