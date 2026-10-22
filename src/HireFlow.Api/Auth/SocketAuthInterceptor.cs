using System.Security.Claims;
using System.Text.Json;
using HotChocolate.AspNetCore;
using HotChocolate.AspNetCore.Subscriptions;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HireFlow.Api.Auth;

/// <summary>
/// Browsers cannot set headers on a WebSocket handshake, so GraphQL clients send the bearer
/// token in the <c>connection_init</c> payload instead. This interceptor validates it once
/// per socket, with the same rules as the HTTP pipeline, so subscriptions get the same
/// <c>[Authorize]</c> checks as queries.
/// </summary>
public sealed class SocketAuthInterceptor : DefaultSocketSessionInterceptor
{
    private static readonly JsonWebTokenHandler TokenHandler = new();

    public override async ValueTask<ConnectionStatus> OnConnectAsync(
        ISocketSession session,
        IOperationMessagePayload connectionInitMessage,
        CancellationToken cancellationToken = default)
    {
        var token = ReadToken(connectionInitMessage);
        if (token is null)
        {
            return ConnectionStatus.Accept();
        }

        // The HTTP authentication result for the handshake is cached per request, so validate
        // the token directly instead of re-running the authentication middleware.
        var context = session.Connection.HttpContext;
        var parameters = context.RequestServices
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;
        var result = await TokenHandler.ValidateTokenAsync(token, parameters);
        if (!result.IsValid)
        {
            return ConnectionStatus.Reject("Invalid or expired access token.");
        }

        context.User = new ClaimsPrincipal(result.ClaimsIdentity);
        return ConnectionStatus.Accept();
    }

    private static string? ReadToken(IOperationMessagePayload message)
    {
        if (message.Payload is not { ValueKind: JsonValueKind.Object } payload)
        {
            return null;
        }

        foreach (var key in new[] { "Authorization", "authorization", "authToken", "token" })
        {
            if (payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var token = value.GetString();
                return token?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true ? token[7..] : token;
            }
        }

        return null;
    }
}
