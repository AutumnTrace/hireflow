using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HireFlow.Api.Auth;

public static class Roles
{
    public const string Recruiter = "Recruiter";
    public const string Admin = "Admin";
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "hireflow";

    public string Audience { get; set; } = "hireflow-api";

    /// <summary>Symmetric signing key; must be at least 32 characters.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Exposes <c>POST /auth/token</c> for local development and tests. Never enable in production.</summary>
    public bool EnableDevTokens { get; set; }

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class AuthSetup
{
    public static IServiceCollection AddHireFlowAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateKey(),
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = "role",
                };
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>Issues short-lived tokens so the API can be explored without an identity provider.</summary>
    public static IEndpointRouteBuilder MapDevTokenEndpoint(this IEndpointRouteBuilder app)
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        if (!options.EnableDevTokens)
        {
            return app;
        }

        app.MapPost("/auth/token", (DevTokenRequest request, IOptions<JwtOptions> jwtOptions) =>
            {
                string[] allowed = [Roles.Recruiter, Roles.Admin];
                var role = request.Role ?? Roles.Recruiter;
                if (!allowed.Contains(role))
                {
                    return Results.BadRequest(new { error = $"Role must be one of: {string.Join(", ", allowed)}." });
                }

                var jwt = jwtOptions.Value;
                var expires = DateTime.UtcNow.AddHours(8);
                var token = new JwtSecurityToken(
                    issuer: jwt.Issuer,
                    audience: jwt.Audience,
                    claims:
                    [
                        new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                        new Claim(JwtRegisteredClaimNames.Name, request.Name ?? "Demo Recruiter"),
                        new Claim("role", role),
                    ],
                    expires: expires,
                    signingCredentials: new SigningCredentials(jwt.CreateKey(), SecurityAlgorithms.HmacSha256));

                return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = expires });
            })
            .WithName("IssueDevToken")
            .AllowAnonymous();

        return app;
    }
}

public sealed record DevTokenRequest(string? Name, string? Role);
