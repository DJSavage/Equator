using Kelvinvale.Api.Auth;
using Kelvinvale.Api.Errors;
using Kelvinvale.Api.Infrastructure;
using Kelvinvale.Core;
using Kelvinvale.Core.Auth;
using Kelvinvale.Core.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHttpContextAccessor();

// Persistence: a single in-memory SQLite connection kept open for the lifetime of the process.
// The in-memory database exists only while a connection to it is open, so this one is the anchor.
var sqliteConnection = new SqliteConnection("DataSource=Kelvinvale;Mode=Memory;Cache=Shared");
sqliteConnection.Open();
builder.Services.AddSingleton(sqliteConnection);
builder.Services.AddDbContext<KelvinvaleDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

builder.Services.AddKelvinvaleCore();

builder.Services.AddAuthentication(StubAuthenticationHandler.SchemeName)
    .AddScheme<StubAuthenticationOptions, StubAuthenticationHandler>(StubAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(PolicyNames.IsAdviser, policy => policy.RequireRole(Roles.Adviser))
    .AddPolicy(PolicyNames.IsCustomer, policy => policy.RequireRole(Roles.Customer));
builder.Services.AddSingleton<IAuthorizationHandler, CanAccessCustomerHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<KelvinvaleDbContext>("database");

builder.Services.AddHostedService<SettlementBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.Run();

/// <summary>Exposed so the integration tests can host the real pipeline via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
