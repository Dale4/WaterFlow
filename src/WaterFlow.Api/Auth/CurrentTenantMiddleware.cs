using WaterFlow.Api.Data;

namespace WaterFlow.Api.Auth;

public sealed class CurrentTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentTenant currentTenant)
    {
        var value = context.User.FindFirst(AuthClaimTypes.TenantId)?.Value;
        if (Guid.TryParse(value, out var tenantId))
            currentTenant.Id = tenantId;

        await next(context);
    }
}
