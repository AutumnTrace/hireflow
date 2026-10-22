namespace HireFlow.Tests.Api;

public sealed class CorsTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Theory]
    [InlineData("/graphql")]
    [InlineData("/auth/token")]
    public async Task Preflight_from_configured_origin_is_allowed(string path)
    {
        var response = await SendPreflightAsync(path, "http://localhost:5173");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal("http://localhost:5173", Assert.Single(origins));
        Assert.Contains("authorization", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant());
    }

    [Fact]
    public async Task Preflight_from_unknown_origin_is_not_allowed()
    {
        var response = await SendPreflightAsync("/graphql", "https://evil.example");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private async Task<HttpResponseMessage> SendPreflightAsync(string path, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await factory.CreateClient().SendAsync(request);
    }
}
