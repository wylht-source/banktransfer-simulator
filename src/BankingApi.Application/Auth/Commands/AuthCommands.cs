namespace BankingApi.Application.Auth.Commands;

public record RegisterCommand(string Name, string Email, string Password);
public record RegisterResult(string UserId, string Email);

public record LoginCommand(string Email, string Password);
public record LoginResult(string Token, string UserId, string Email);
