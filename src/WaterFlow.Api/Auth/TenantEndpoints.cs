using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var admins = app.MapGroup("").RequireAuthorization("PlatformAdmin");
        admins.MapGet("/tenants", ListTenants);
        admins.MapPost("/tenants", CreateTenant);
        admins.MapPatch("/tenants/{tenantId:guid}", RenameTenant);
        admins.MapDelete("/tenants/{tenantId:guid}", DeleteTenant);
        admins.MapPost("/tenants/{tenantId:guid}/operators", AddOperator);
        admins.MapPost("/tenants/{tenantId:guid}/users", CreateUser);
        admins.MapPatch("/tenants/{tenantId:guid}/users/{userId}", UpdateUser);
        admins.MapDelete("/tenants/{tenantId:guid}/users/{userId}", DeleteUser);
        admins.MapGet("/memberships", ListMemberships);
    }

    private static async Task<IResult> ListTenants(WaterFlowDbContext db)
    {
        var rows = await db.Tenants
            .OrderBy(tenant => tenant.Name)
            .Select(tenant => new TenantResponse(tenant.Id, tenant.Name))
            .ToListAsync();
        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var name = request.Name?.Trim() ?? "";
        var userName = request.OperatorUserName?.Trim() ?? "";
        if (name.Length == 0 || userName.Length == 0 || string.IsNullOrWhiteSpace(request.OperatorPassword))
            return Results.Json(new ErrorResponse("invalid_tenant"), statusCode: StatusCodes.Status400BadRequest);

        var nameTaken = await db.Tenants.AnyAsync(tenant => tenant.Name.ToLower() == name.ToLower());
        if (nameTaken)
            return Results.Json(new ErrorResponse("tenant_name_in_use"), statusCode: StatusCodes.Status409Conflict);

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = name };
        var added = await AddOperatorMembershipAsync(users, db, tenant, userName, request.OperatorPassword, createTenant: true);
        if (added.Error is not null)
            return added.Error;

        return Results.Created($"/tenants/{tenant.Id}", new CreatedTenantResponse(tenant.Id, tenant.Name, added.UserId!));
    }

    private static async Task<IResult> AddOperator(
        Guid tenantId,
        AddOperatorRequest request,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId);
        if (tenant is null)
            return Results.NotFound();

        var userName = request.UserName?.Trim() ?? "";
        if (userName.Length == 0)
            return Results.Json(new ErrorResponse("invalid_user"), statusCode: StatusCodes.Status400BadRequest);

        var added = await AddOperatorMembershipAsync(users, db, tenant, userName, request.Password, createTenant: false);
        if (added.Error is not null)
            return added.Error;

        return Results.Created(
            $"/tenants/{tenant.Id}/operators/{added.UserId}",
            new CreatedOperatorResponse(added.UserId!, tenant.Id));
    }

    private static async Task<IResult> RenameTenant(Guid tenantId, RenameTenantRequest request, WaterFlowDbContext db)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
            return Results.Json(new ErrorResponse("invalid_tenant"), statusCode: StatusCodes.Status400BadRequest);

        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId);
        if (tenant is null)
            return Results.NotFound();

        var nameTaken = await db.Tenants.AnyAsync(item =>
            item.Id != tenantId && item.Name.ToLower() == name.ToLower());
        if (nameTaken)
            return Results.Json(new ErrorResponse("tenant_name_in_use"), statusCode: StatusCodes.Status409Conflict);

        tenant.Name = name;
        await db.SaveChangesAsync();
        return Results.Ok(new TenantResponse(tenant.Id, tenant.Name));
    }

    private static async Task<IResult> DeleteTenant(
        Guid tenantId,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId);
        if (tenant is null)
            return Results.NotFound();

        var memberships = await db.Memberships.Where(membership => membership.TenantId == tenantId).ToListAsync();
        var memberIds = memberships.Select(membership => membership.UserId).Distinct().ToList();
        db.Memberships.RemoveRange(memberships);
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync();

        foreach (var memberId in memberIds)
            await AccountCleanup.DeleteIfUnusedAsync(users, db, memberId);

        return Results.NoContent();
    }

    private static async Task<IResult> CreateUser(
        Guid tenantId,
        CreateTenantUserRequest request,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        if (await db.Tenants.SingleOrDefaultAsync(item => item.Id == tenantId) is null)
            return Results.NotFound();

        var email = request.Email?.Trim() ?? "";
        if (email.Length == 0 || string.IsNullOrWhiteSpace(request.Password))
            return Results.Json(new ErrorResponse("invalid_user"), statusCode: StatusCodes.Status400BadRequest);
        if (!Enum.TryParse<MemberRole>(request.Role, ignoreCase: true, out var role))
            return Results.Json(new ErrorResponse("invalid_role"), statusCode: StatusCodes.Status400BadRequest);
        if (await users.FindByEmailAsync(email) is not null)
            return Results.Json(new ErrorResponse("email_in_use"), statusCode: StatusCodes.Status409Conflict);

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
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
        return Results.Created(
            $"/tenants/{tenantId}/users/{user.Id}",
            new CreatedMemberResponse(user.Id, user.Email ?? email, tenantId, role.ToString()));
    }

    private static async Task<IResult> UpdateUser(
        Guid tenantId,
        string userId,
        UpdateMembershipRequest request,
        WaterFlowDbContext db)
    {
        if (!Enum.TryParse<MemberRole>(request.Role, ignoreCase: true, out var role))
            return Results.Json(new ErrorResponse("invalid_role"), statusCode: StatusCodes.Status400BadRequest);

        var membership = await db.Memberships.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.UserId == userId);
        if (membership is null)
            return Results.NotFound();

        membership.Role = role;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteUser(
        Guid tenantId,
        string userId,
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db)
    {
        var membership = await db.Memberships.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.UserId == userId);
        if (membership is null)
            return Results.NotFound();

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync();
        await AccountCleanup.DeleteIfUnusedAsync(users, db, userId);
        return Results.NoContent();
    }

    private static async Task<IResult> ListMemberships(WaterFlowDbContext db)
    {
        var rows = await (
            from membership in db.Memberships
            join user in db.Users on membership.UserId equals user.Id
            join tenant in db.Tenants on membership.TenantId equals tenant.Id
            orderby tenant.Name, user.Email
            select new
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                membership.Role
            }
        ).ToListAsync();

        return Results.Ok(rows.Select(row =>
            new MembershipResponse(row.UserId, row.Email, row.TenantId, row.TenantName, row.Role.ToString())));
    }

    private static async Task<(IResult? Error, string? UserId)> AddOperatorMembershipAsync(
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db,
        Tenant tenant,
        string userName,
        string? password,
        bool createTenant)
    {
        var user = await users.FindByEmailAsync(userName);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest), null);

            user = new ApplicationUser { UserName = userName, Email = userName, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
                return (Results.Json(new ErrorResponse("invalid_password"), statusCode: StatusCodes.Status400BadRequest), null);
        }
        else if (await db.Memberships.AnyAsync(membership => membership.UserId == user.Id && membership.TenantId == tenant.Id))
        {
            return (Results.Json(new ErrorResponse("already_member"), statusCode: StatusCodes.Status409Conflict), null);
        }

        if (createTenant)
            db.Tenants.Add(tenant);

        db.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = MemberRole.Operator
        });
        await db.SaveChangesAsync();
        return (null, user.Id);
    }
}

public sealed record CreateTenantRequest(string Name, string OperatorUserName, string OperatorPassword);

public sealed record CreatedTenantResponse(Guid TenantId, string Name, string OperatorUserId);

public sealed record AddOperatorRequest(string UserName, string? Password);

public sealed record CreatedOperatorResponse(string UserId, Guid TenantId);

public sealed record TenantResponse(Guid Id, string Name);

public sealed record RenameTenantRequest(string? Name);

public sealed record CreateTenantUserRequest(string? Email, string? Password, string? Role);

public sealed record UpdateMembershipRequest(string? Role);

public sealed record CreatedMemberResponse(string UserId, string Email, Guid TenantId, string Role);

public sealed record MembershipResponse(string UserId, string Email, Guid TenantId, string TenantName, string Role);

internal static class AccountCleanup
{
    public static async Task DeleteIfUnusedAsync(
        UserManager<ApplicationUser> users,
        WaterFlowDbContext db,
        string userId)
    {
        if (await db.Memberships.AnyAsync(membership => membership.UserId == userId))
            return;
        if (await db.PlatformAdmins.AnyAsync(admin => admin.UserId == userId))
            return;

        var user = await users.FindByIdAsync(userId);
        if (user is not null)
            await users.DeleteAsync(user);
    }
}
