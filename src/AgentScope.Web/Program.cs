using AgentScope.Infrastructure;
using AgentScope.Infrastructure.Persistence;
using AgentScope.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Infrastructure: DbContext (scoped) + repositories — used by API-like services
builder.Services.AddInfrastructure(builder.Configuration);

// DbContext factory for Blazor Server components (avoids cross-lifetime disposal issues)
var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");
builder.Services.AddDbContextFactory<AgentScopeDbContext>(options =>
    options.UseNpgsql(connStr, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(AgentScopeDbContext).Assembly.FullName);
        npgsql.EnableRetryOnFailure(3);
    }), ServiceLifetime.Scoped);

// Dashboard query service — uses factory, safe for Blazor Server circuit lifetime
builder.Services.AddScoped<DashboardService>();
builder.Services.AddHttpClient();

// Auth services
// AddAuthentication registers IAuthenticationService (required by middleware even in Blazor Server)
// Actual auth is handled by JwtAuthStateProvider + AuthorizeRouteView, not HTTP middleware
builder.Services.AddAuthentication();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<CurrentUserService>();

// HttpClient for API calls
builder.Services.AddHttpClient("AgentScopeApi", client =>
{
    var apiUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:7000";
    client.BaseAddress = new Uri(apiUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// AllowAnonymous at HTTP level: auth is handled by Blazor's AuthorizeRouteView + JwtAuthStateProvider
// [Authorize] on pages is evaluated by AuthorizeRouteView (not HTTP middleware)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", ts = DateTime.UtcNow }));

app.MapRazorComponents<AgentScope.Web.Components.App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.Run();
