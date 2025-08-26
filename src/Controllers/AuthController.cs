using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedConnect.Features.Auth.Commands;
using MedConnect.Features.Users.Commands;
using MedConnect.Services;
using WebApiProject.Features.Auth.Commands;
using Polly.CircuitBreaker;

namespace MedConnect.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;
    private readonly ICircuitBreakerService _circuitBreakerService;

    public AuthController(IMediator mediator, ICircuitBreakerService circuitBreakerService, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _circuitBreakerService = circuitBreakerService;
        _logger = logger;
    }

    [HttpPost("test-circuit-breaker")]
    public async Task<IActionResult> TestCircuitBreaker()
    {
        try
        {
            await _circuitBreakerService.ExecuteAsync(async () =>
            {
                // Simulate an operation that might fail
                throw new Exception("Simulated failure");
            });
    
            return Ok("Operation succeeded");
        }
        catch (BrokenCircuitException)
        {
            return StatusCode(503, "Circuit is open. Please try again later.");
        }
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] CreateUserCommand command)
    {
        _logger.LogInformation("Registering new user: {Username}", command.Username);
        var user = await _mediator.Send(command);
        _logger.LogInformation("User registered successfully: {Username}", command.Username);
        return Ok(new { message = "User registered successfully" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        _logger.LogInformation("User login attempt: {Username}", command.Username);
        var result = await _mediator.Send(command);
        if (result == null)
        {
            _logger.LogWarning("Invalid login attempt for user: {Username}", command.Username);
            return Unauthorized(new { message = "Invalid username or password" });
        }
        _logger.LogInformation("User logged in successfully: {Username}", command.Username);
        // Set JWT as HttpOnly/Secure cookie
        Response.Cookies.Append("jwt", result.Value.Jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });
        return Ok(new { token = result.Value.Jwt, refreshToken = result.Value.RefreshToken, role = result.Value.role });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        _logger.LogInformation("Refresh token attempt");
        var result = await _mediator.Send(command);
        if (result == null)
        {
            _logger.LogWarning("Invalid refresh token attempt");
            return Unauthorized(new { message = "Invalid refresh token" });
        }
        _logger.LogInformation("Token refreshed successfully");
        // Set new JWT as HttpOnly/Secure cookie
        Response.Cookies.Append("jwt", result.Value.Jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });
        return Ok(new { token = result.Value.Jwt, refreshToken = result.Value.RefreshToken });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        _logger.LogInformation("Forgot password attempt for email: {Email}", command.Email);
        var result = await _mediator.Send(command);
        if (!result)
        {
            _logger.LogWarning("Forgot password failed: email not found: {Email}", command.Email);
            return NotFound(new { message = "Email not found" });
        }
        _logger.LogInformation("Password reset instructions sent for email: {Email}", command.Email);
        return Ok(new { message = "Password reset instructions sent if email exists" });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var jti = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        var username = User.Identity?.Name;
        if (!string.IsNullOrEmpty(jti) && !string.IsNullOrEmpty(username))
        {
            var userRepo = HttpContext.RequestServices.GetService(typeof(MedConnect.Repositories.IUserRepository)) as MedConnect.Repositories.IUserRepository;
            if (userRepo != null)
            {
                var user = await userRepo.GetByUsernameAsync(username);
                if (user != null)
                {
                    await userRepo.AddRevokedJtiAsync(user, jti);
                    _logger.LogInformation("JTI {Jti} revoked for user {Username}", jti, username);
                }
            }
        }
        Response.Cookies.Delete("jwt");
        _logger.LogInformation("User logged out and JWT cookie deleted");
        return Ok(new { message = "Logged out" });
    }
}