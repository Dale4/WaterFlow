using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public static class PlatformAdminEndpoints
{
    public static void MapPlatformAdminEndpoints(this WebApplication app)
    {
        app.MapPost("/platform-admins", Create).RequireAuthorization("PlatformAdmin");
        app.MapDelete("/platform-admins/{userId}", Delete).RequireAuthorization("PlatformAdmin");
    }

    private static async Task<IResult> Create(
        CreatePlatformAdminRequest request,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0)
            return Results.Json(new ErrorResponse("invalid_user"), statusCode: StatusCodes.Status400BadRequest);

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest);

            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
                return Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest);
        }
        else if (await db.PlatformAdmins.AnyAsync(admin => admin.UserId == user.Id))
        {
            return Results.Json(new ErrorResponse("already_platform_admin"), statusCode: StatusCodes.Status409Conflict);
        }

        db.PlatformAdmins.Add(new PlatformAdmin { UserId = user.Id });
        await db.SaveChangesAsync();
        return Results.Created($"/platform-admins/{user.Id}", new CreatedPlatformAdminResponse(user.Id, user.Email ?? email));
    }

    private static async Task<IResult> Delete(
        string userId,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var admins = await db.PlatformAdmins.ToListAsync();
        var admin = admins.SingleOrDefault(item => item.UserId == userId);
        if (admin is null)
            return Results.NotFound();
        if (admins.Count <= 1)
            return Results.Json(new ErrorResponse("last_platform_admin"), statusCode: StatusCodes.Status409Conflict);

        var hasMembership = await db.Memberships.AnyAsync(membership => membership.UserId == userId);
        db.PlatformAdmins.Remove(admin);
        await db.SaveChangesAsync();

        if (!hasMembership)
        {
            var user = await users.FindByIdAsync(userId);
            if (user is not null)
                await users.DeleteAsync(user);
        }

        return Results.NoContent();
    }
}

public sealed record CreatePlatformAdminRequest(string Email, string? Password);

public sealed record CreatedPlatformAdminResponse(string UserId, string Email);
