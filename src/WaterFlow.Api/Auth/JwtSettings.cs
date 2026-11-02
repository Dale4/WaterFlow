namespace WaterFlow.Api.Auth;

public static class JwtSettings
{
    public static (string SigningKey, string Issuer, string Audience) Resolve(IConfiguration configuration)
    {
        var signingKey = First(configuration, "Jwt:SigningKey", "JWT_SIGNING_KEY");
        var issuer = First(configuration, "Jwt:Issuer", "JWT_ISSUER");
        var audience = First(configuration, "Jwt:Audience", "JWT_AUDIENCE");

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Set Jwt:SigningKey in User Secrets or JWT_SIGNING_KEY to at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Set Jwt:Issuer and Jwt:Audience, or JWT_ISSUER and JWT_AUDIENCE.");
        }

        return (signingKey, issuer, audience);
    }

    private static string? First(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}
