using MediatR;
using MedConnect.Repositories;
using MedConnect.Services;
using System.Security.Cryptography;
using MedConnect.Models;

namespace WebApiProject.Features.Auth.Commands;

public record LoginCommand(string Username, string Password) : IRequest<(string Jwt, string RefreshToken, Role role)?>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, (string Jwt, string RefreshToken, Role role)?>
{
    private readonly IUserRepository _userRepository;
    private readonly JwtService _jwtService;

    public LoginCommandHandler(IUserRepository userRepository, JwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<(string Jwt, string RefreshToken, Role role)?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        // Verify password
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid) return null;

        // Generate JWT token
        var jwt = _jwtService.GenerateToken(user);
        // Generate secure refresh token
        var refreshToken = new MedConnect.Models.RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow,
            UserId = user.Id
        };
        await _userRepository.AddRefreshTokenAsync(user, refreshToken);
        await _userRepository.RemoveOldRefreshTokensAsync(user);
        return (jwt, refreshToken.Token, user.Role);
    }
}