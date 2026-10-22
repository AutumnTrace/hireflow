using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HireFlow.Api.Auth;

namespace HireFlow.Tests.Api;

/// <summary>Drives a real <c>graphql-transport-ws</c> session against the in-memory test server.</summary>
public sealed class SubscriptionTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Stage_changes_are_pushed_to_subscribers()
    {
        var client = await factory.CreateGraphQLClientAsync(Roles.Recruiter);
        var token = client.Http.DefaultRequestHeaders.Authorization!.Parameter!;
        var target = (await client.ExecuteAsync(
                "{ applications(where: { stage: { eq: APPLIED } }, first: 1) { nodes { id job { id } } } }"))
            .At("data.applications.nodes.0");
        var applicationId = target.At("id").GetString();
        var jobId = target.At("job.id").GetString();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var wsClient = factory.Server.CreateWebSocketClient();
        wsClient.SubProtocols.Add("graphql-transport-ws");
        using var socket = await wsClient.ConnectAsync(new Uri(factory.Server.BaseAddress, "graphql"), timeout.Token);

        await SendAsync(socket, new { type = "connection_init", payload = new { Authorization = $"Bearer {token}" } }, timeout.Token);
        Assert.Equal("connection_ack", (await ReceiveAsync(socket, timeout.Token)).At("type").GetString());

        await SendAsync(socket, new
        {
            id = "1",
            type = "subscribe",
            payload = new
            {
                query = $$"""subscription { onApplicationStageChanged(jobPostingId: "{{jobId}}") { from to note } }""",
            },
        }, timeout.Token);

        // Give the server a moment to register the subscription before publishing.
        await Task.Delay(300, timeout.Token);
        await client.ExecuteAsync($$"""
            mutation { moveApplication(input: { applicationId: "{{applicationId}}", stage: SCREENING, note: "via test" }) {
              jobApplication { id } } }
            """);

        var message = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal("next", message.At("type").GetString());
        var change = message.At("payload.data.onApplicationStageChanged");
        Assert.Equal("APPLIED", change.At("from").GetString());
        Assert.Equal("SCREENING", change.At("to").GetString());
        Assert.Equal("via test", change.At("note").GetString());
    }

    [Fact]
    public async Task Invalid_socket_token_is_rejected()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var wsClient = factory.Server.CreateWebSocketClient();
        wsClient.SubProtocols.Add("graphql-transport-ws");
        using var socket = await wsClient.ConnectAsync(new Uri(factory.Server.BaseAddress, "graphql"), timeout.Token);

        await SendAsync(socket, new { type = "connection_init", payload = new { Authorization = "Bearer not-a-jwt" } }, timeout.Token);

        var buffer = new byte[1024];
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
    }

    private static Task SendAsync(WebSocket socket, object message, CancellationToken ct)
        => socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, endOfMessage: true, ct);

    private static async Task<JsonElement> ReceiveAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            stream.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var element = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray())).RootElement;
            stream.SetLength(0);

            // Skip keep-alive pings; the protocol allows them at any time.
            if (element.At("type").GetString() is "ping" or "pong")
            {
                continue;
            }

            return element;
        }
    }
}
