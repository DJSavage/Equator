using System.Net;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests;

public sealed class HealthTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Health_endpoint_is_anonymous_and_reports_healthy()
    {
        var response = await Factory.Anonymous().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
