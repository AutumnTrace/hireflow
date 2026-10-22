using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HireFlow.Tests.Api;

public sealed class HealthCheckTests(HireFlowApiFactory factory) : IClassFixture<HireFlowApiFactory>
{
    [Fact]
    public async Task Liveness_probe_does_not_touch_dependencies()
    {
        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.At("status").GetString());
        Assert.Equal(0, body.At("checks").GetArrayLength());
    }

    [Fact]
    public async Task Readiness_probe_checks_the_database()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var check = Assert.Single(body.At("checks").EnumerateArray());
        Assert.Equal("database", check.At("name").GetString());
        Assert.Equal("Healthy", check.At("status").GetString());
    }
}
