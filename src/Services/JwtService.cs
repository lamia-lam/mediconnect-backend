using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using MedConnect.Models;

namespace MedConnect.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;
    private readonly RSA _privateRsa;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        var privateKeyPem = _configuration["Jwt:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new InvalidOperationException("JWT PrivateKeyPem is not configured");
        _privateRsa = RSA.Create();
        _privateRsa.ImportFromPem(privateKeyPem.ToCharArray());
    }

    public string GenerateToken(User user)
    {
        _logger.LogInformation("Generating JWT token for user: {Username}", user.Username);
        var credentials = new SigningCredentials(new RsaSecurityKey(_privateRsa), SecurityAlgorithms.RsaSha256);

        var jti = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials);

        _logger.LogInformation("JWT token generated for user: {Username}, JTI: {Jti}", user.Username, jti);
        foreach (var claim in claims)
        {
            _logger.LogDebug("JWT claim: {Type} = {Value}", claim.Type, claim.Value);
        }
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}