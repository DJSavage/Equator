using Kelvinvale.Api.Infrastructure;
using Kelvinvale.Core.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kelvinvale.Tests.Support;

/// <summary>
/// Hosts the real API pipeline for integration tests against a private in-memory SQLite database
/// and a controllable clock. The timer-driven settlement service is removed; tests drive
/// <c>ISettlementRunner</c> directly so settlement is deterministic.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory() => _connection.Open();

    /// <summary>The instant the seed data is anchored to: 30 Aug 2026, tax year 2026/27.</summary>
    public static readonly DateTimeOffset SeedInstant = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Fixed at <see cref="SeedInstant"/>. Tests advance it to exercise the tax-year boundary.</summary>
    public FakeTimeProvider Clock { get; } = new(SeedInstant);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SqliteConnection>();
            services.AddSingleton(_connection);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            var settlement = services.SingleOrDefault(d => d.ImplementationType == typeof(SettlementBackgroundService));
            if (settlement is not null)
            {
                services.Remove(settlement);
            }
        });
    }

    /// <summary>Resets the clock and reseeds the database so each test starts from the known seed set.</summary>
    public async Task ResetAsync()
    {
        if (Clock.GetUtcNow() != SeedInstant)
        {
            Clock.SetUtcNow(SeedInstant);
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        DbSeeder.Seed(db, Clock);
    }

    public async Task<T> WithDbAsync<T>(Func<KelvinvaleDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();
        return await action(db);
    }

    public async Task RunSettlementAsync()
    {
        using var scope = Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<Kelvinvale.Core.Application.ISettlementRunner>();
        await runner.RunDueAsync(CancellationToken.None);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
