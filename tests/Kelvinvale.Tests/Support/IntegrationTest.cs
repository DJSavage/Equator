namespace Kelvinvale.Tests.Support;

/// <summary>Base for integration tests: one hosted API per test class, reseeded before each test.</summary>
public abstract class IntegrationTest(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
