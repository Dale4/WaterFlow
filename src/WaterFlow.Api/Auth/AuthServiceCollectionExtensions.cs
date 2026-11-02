using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddWaterFlowAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<WaterFlowDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var (signingKey, issuer, audience) = JwtSettings.Resolve(configuration);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = AuthClaimTypes.Role,
                    NameClaimType = JwtRegisteredClaimNames.Sub
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Operator", policy => policy.RequireRole(nameof(MemberRole.Operator)));
            options.AddPolicy("CanWrite", policy => policy.RequireRole(
                nameof(MemberRole.Operator),
                nameof(MemberRole.BillingClerk)));
            options.AddPolicy("TenantRead", policy =>
                policy.RequireAuthenticatedUser().RequireClaim(AuthClaimTypes.TenantId));
        });

        services.AddSingleton<JwtTokenService>();
        return services;
    }
}
