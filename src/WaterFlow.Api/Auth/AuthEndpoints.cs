using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", Login);
        group.MapPost("/forgot-password", ForgotPassword);
        group.MapPost("/reset-password", ResetPassword);
        group.MapGet("/me", Me).RequireAuthorization("TenantRead");
        group.MapPost("/change-password", ChangePassword).RequireAuthorization("TenantRead");
        group.MapPost("/users", CreateUser).RequireAuthorization("Operator");
        group.MapPost("/users/{userId}/password", SetPassword).RequireAuthorization("Operator");
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        WaterFlowDbContext db,
        JwtTokenService tokens)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized("invalid_credentials");

        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized("invalid_credentials");

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Unauthorized("locked_out");
        if (!result.Succeeded)
            return Unauthorized("invalid_credentials");

        var memberships = await db.Memberships
            .Where(membership => membership.UserId == user.Id)
            .ToListAsync();

        Membership? membership;
        if (request.TenantId is Guid tenantId)
            membership = memberships.SingleOrDefault(item => item.TenantId == tenantId);
        else if (memberships.Count == 1)
            membership = memberships[0];
        else
            return Results.Json(new ErrorResponse("tenant_required"), statusCode: StatusCodes.Status400BadRequest);

        if (membership is null)
            return Unauthorized("invalid_credentials");

        var (token, expiresAt) = tokens.Create(user, membership);
        return Results.Ok(new LoginResponse(token, expiresAt, membership.TenantId, membership.Role.ToString()));
    }

    private static async Task<IResult> ForgotPassword(ForgotPasswordRequest request, UserManager<ApplicationUser> users)
    {
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var user = await users.FindByEmailAsync(request.Email);
            if (user is not null)
                _ = await users.GeneratePasswordResetTokenAsync(user);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, UserManager<ApplicationUser> users)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email);
        if (user is null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return Results.Json(new ErrorResponse("reset_failed"), statusCode: StatusCodes.Status400BadRequest);

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return Results.Json(new ErrorResponse("reset_failed"), statusCode: StatusCodes.Status400BadRequest);

        return Results.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, WaterFlowDbContext db)
    {
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var role = principal.FindFirst(AuthClaimTypes.Role)?.Value;
        if (!TryGetTenantId(principal, out var tenantId))
            return Results.Unauthorized();

        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId);
        return Results.Ok(new MeResponse(userId, email, tenantId, tenant?.Name, role));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users)
    {
        var user = await FindCallerAsync(principal, users);
        if (user is null)
            return Results.Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Results.Json(new ErrorResponse("password_change_failed"), statusCode: StatusCodes.Status400BadRequest);

        return Results.NoContent();
    }

    private static async Task<IResult> CreateUser(
        CreateUserRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        if (!TryGetTenantId(principal, out var tenantId))
            return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.Json(new ErrorResponse("invalid_user"), statusCode: StatusCodes.Status400BadRequest);
        if (!Enum.TryParse<MemberRole>(request.Role, ignoreCase: true, out var role))
            return Results.Json(new ErrorResponse("invalid_role"), statusCode: StatusCodes.Status400BadRequest);

        if (await users.FindByEmailAsync(request.Email) is not null)
            return Results.Json(new ErrorResponse("email_in_use"), statusCode: StatusCodes.Status409Conflict);

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest);

        db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenantId,
            Role = role
        });
        await db.SaveChangesAsync();

        return Results.Created($"/auth/users/{user.Id}", new CreatedUserResponse(user.Id, user.Email, tenantId, role.ToString()));
    }

    private static async Task<IResult> SetPassword(
        string userId,
        SetPasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        if (!TryGetTenantId(principal, out var tenantId))
            return Results.Unauthorized();

        var inTenant = await db.Memberships.AnyAsync(membership =>
            membership.UserId == userId && membership.TenantId == tenantId);
        if (!inTenant)
            return Results.NotFound();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest);

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest);

        return Results.NoContent();
    }

    private static async Task<ApplicationUser?> FindCallerAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await users.FindByIdAsync(userId);
    }

    private static bool TryGetTenantId(ClaimsPrincipal principal, out Guid tenantId)
    {
        var value = principal.FindFirst(AuthClaimTypes.TenantId)?.Value;
        return Guid.TryParse(value, out tenantId);
    }

    private static IResult Unauthorized(string error) =>
        Results.Json(new ErrorResponse(error), statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record LoginRequest(string Email, string Password, Guid? TenantId);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, Guid TenantId, string Role);

public sealed record ErrorResponse(string Error);

public sealed record MeResponse(string? UserId, string? Email, Guid TenantId, string? TenantName, string? Role);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record CreateUserRequest(string Email, string Password, string Role);

public sealed record CreatedUserResponse(string Id, string? Email, Guid TenantId, string Role);

public sealed record SetPasswordRequest(string NewPassword);
