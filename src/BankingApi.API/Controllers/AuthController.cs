using BankingApi.Application.Auth.Commands;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;


namespace BankingApi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserManager<IdentityUser> userManager, IConfiguration config, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _config = config;
        _logger = logger;
    }

    /// <summary>Register a new user. All users are assigned the Client role by default.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("register-policy")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand cmd)
    {
        var user = new IdentityUser { UserName = cmd.Email, Email = cmd.Email };
        var result = await _userManager.CreateAsync(user, cmd.Password);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // All registered users get the Client role by default
        await _userManager.AddToRoleAsync(user, "Client");

        return CreatedAtAction(nameof(Register), new RegisterResult(user.Id, user.Email!));
    }

    /// <summary>Login and receive a JWT token.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("login-policy")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd)
    {
        var user = await _userManager.FindByEmailAsync(cmd.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, cmd.Password))
        {
            _logger.LogWarning(
            "LoginFailed — Email: {Email}, IP: {IP}",
            cmd.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);
        return Ok(new LoginResult(token, user.Id, user.Email!));
    }

    private string GenerateJwtToken(IdentityUser user, IList<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add roles to JWT claims — used for authorization in loan approval
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}