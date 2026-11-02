using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public sealed class JwtTokenService
{
    public const int LifetimeMinutes = 30;

    private readonly string _signingKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(IConfiguration configuration)
    {
        (_signingKey, _issuer, _audience) = JwtSettings.Resolve(configuration);
    }

    public (string Token, DateTimeOffset ExpiresAt) Create(ApplicationUser user, Membership membership)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(LifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(AuthClaimTypes.TenantId, membership.TenantId.ToString()),
                new Claim(AuthClaimTypes.Role, membership.Role.ToString())
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
