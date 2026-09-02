using Kelvinvale.Api.Auth;
using Kelvinvale.Api.Errors;
using Kelvinvale.Api.Infrastructure;
using Kelvinvale.Api.OpenApi;
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
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<CallerSecuritySchemeTransformer>());

//registers ASP.NET Core’s built‑in Problem Details service so the API automatically returns RFC‑9457
//compliant error responses instead of inconsistent or empty error bodies.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

//registers the IHttpContextAccessor service so we can access the current HttpContext from any class in the application —
//not just controllers or middleware.
builder.Services.AddHttpContextAccessor();

// Persistence: a single in-memory SQLite connection kept open for the lifetime of the process.
// The in-memory database exists only while a connection to it is open, so this one is the anchor.
var sqliteConnection = new SqliteConnection("DataSource=Kelvinvale;Mode=Memory;Cache=Shared");
sqliteConnection.Open();
builder.Services.AddSingleton(sqliteConnection);
builder.Services.AddDbContext<KelvinvaleDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));


builder.Services.AddKelvinvaleCore();

//registers the Kelvinvale.Api.Auth.StubAuthenticationHandler as the authentication scheme for the application.
//This handler reads custom headers from incoming requests to simulate authentication,
//allowing developers to test the API without a real identity provider.
//Identity ids can be found in Kelvinvale.Core.Persistence.SeedIds and used for testing like:
// "X-Caller-Id: 00000000-0000-0000-0000-000000000001 X-Caller-Role: Customer | Adviser"
builder.Services.AddAuthentication(StubAuthenticationHandler.SchemeName)
                .AddScheme<StubAuthenticationOptions, StubAuthenticationHandler>(StubAuthenticationHandler.SchemeName, _ => { });


//registers the authorization policies for the application. It sets a fallback policy that requires all requests to be authenticated by default.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(PolicyNames.IsAdviser, policy => policy.RequireRole(Roles.Adviser))
    .AddPolicy(PolicyNames.IsCustomer, policy => policy.RequireRole(Roles.Customer));

//registers the Kelvinvale.Api.Auth.CanAccessCustomerHandler as a singleton service that implements the IAuthorizationHandler interface.
builder.Services.AddSingleton<IAuthorizationHandler, CanAccessCustomerHandler>();


//registers a health check for the KelvinvaleDbContext to ensure that the database is available and functioning correctly.
builder.Services.AddHealthChecks().AddDbContextCheck<KelvinvaleDbContext>("database");

//registers the Kelvinvale.Api.Infrastructure.SettlementBackgroundService as a hosted service that runs in the background of the application.
//settlement date here is set as today + 2 working days, and the service runs every 15 seconds to check for due settlements and process them.
builder.Services.AddHostedService<SettlementBackgroundService>();

var app = builder.Build();

// Ensure the database is created and seeded with initial data.
// This is done within a scoped service provider to ensure that the DbContext is properly disposed of after use.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();
    // EnsureCreated is used here to create the database if it does not exist. In a production scenario, you might use migrations instead.
    // This is a simple approach for an in-memory database used for testing and development.
    //EF builds the model from Kelvinvale.Core.Persistence.KelvinvaleDbContext 
    db.Database.EnsureCreated();
    //seeds the database. guards against multiple calls to Seed by checking if the database already has some Advisers.
    //If it does, it does nothing; otherwise, it populates the database with initial data.
    DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    // API docs, before authentication so the fallback "must be authenticated" policy does not gate them.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();

    // Swagger UI served from the built-in OpenAPI document (no second doc generator).
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Kelvinvale Wealth API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Kelvinvale Wealth API";
    });
}

app.UseAuthentication();
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Exposed so the integration tests can host the real pipeline via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
