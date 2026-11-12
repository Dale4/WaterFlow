using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WaterFlow.Api.Data;
using WaterFlow.Api.Identity;

namespace WaterFlow.Api.Auth;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        app.MapPost("/tenants", CreateTenant).RequireAuthorization("PlatformAdmin");
        app.MapPost("/tenants/{tenantId:guid}/operators", AddOperator).RequireAuthorization("PlatformAdmin");
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
