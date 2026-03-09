using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using AgentScope.Api;
using AgentScope.Api.Agents;
using AgentScope.Api.Middleware;
using AgentScope.Infrastructure;
using AgentScope.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT env var — override Kestrel default
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Infrastructure (DbContext, repositories, UoW)
builder.Services.AddInfrastructure(builder.Configuration);

// ASP.NET Core Identity (requires Microsoft.NET.Sdk.Web — stays in API layer)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AgentScopeDbContext>()
    .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings.GetValue<string>("Key") ?? "SuperSecretKeyForDevelopmentOnly1234567890!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

// In-memory cache for SSH challenges
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SshChallengeStore>();

// Agents (scoped — they hold scoped repository references)
builder.Services.AddScoped<TraceIngestionAgent>();
builder.Services.AddScoped<MetricsAggregationAgent>();

// Retention background job
builder.Services.AddHostedService<RetentionCleanupService>();

// Rate limiter — sliding window per api-key (per-second bursts)
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("per-api-key", limiterOptions =>
    {
        limiterOptions.PermitLimit        = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 100);
        limiterOptions.Window             = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimit:WindowSeconds", 60));
        limiterOptions.SegmentsPerWindow  = 6;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit         = 10;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── App pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

// Schema bootstrap: EnsureCreated + idempotent column additions (ARM64 safe, no migrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AgentScope.Infrastructure.Persistence.AgentScopeDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE users ADD COLUMN IF NOT EXISTS ssh_public_key TEXT;
        ALTER TABLE users ADD COLUMN IF NOT EXISTS git_hub_id VARCHAR(64);
        ALTER TABLE spans ADD COLUMN IF NOT EXISTS railway_op VARCHAR(20);
        ALTER TABLE spans ADD COLUMN IF NOT EXISTS result_is_success BOOLEAN;
        ALTER TABLE spans ADD COLUMN IF NOT EXISTS railway_error_code VARCHAR(200);
        CREATE TABLE IF NOT EXISTS token_usages (
            id               UUID          PRIMARY KEY,
            span_id          UUID          NOT NULL,
            application_id   UUID          NOT NULL,
            model            VARCHAR(100)  NOT NULL,
            prompt_tokens    INT           NOT NULL,
            completion_tokens INT          NOT NULL,
            cost_usd         NUMERIC(18,8) NOT NULL,
            recorded_at      TIMESTAMPTZ   NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_token_usages_application_id ON token_usages(application_id);
        CREATE INDEX IF NOT EXISTS ix_token_usages_recorded_at    ON token_usages(recorded_at);
        CREATE TABLE IF NOT EXISTS prompts (
            id               UUID          PRIMARY KEY,
            application_id   UUID          NOT NULL,
            slug             VARCHAR(200)  NOT NULL,
            version          INT           NOT NULL,
            content          TEXT          NOT NULL,
            change_note      VARCHAR(500),
            is_active        BOOLEAN       NOT NULL DEFAULT FALSE,
            published_at     TIMESTAMPTZ   NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_prompts_app_slug_version ON prompts(application_id, slug, version);
        CREATE INDEX IF NOT EXISTS ix_prompts_app_slug_active ON prompts(application_id, slug, is_active);
        ALTER TABLE subscriptions ADD COLUMN IF NOT EXISTS stripe_customer_id VARCHAR(64);
        """);
    if (app.Environment.IsDevelopment())
        await DevDataSeeder.SeedAsync(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", ts = DateTime.UtcNow }));

app.UseSwagger();
app.UseSwaggerUI();

app.UseRateLimiter();
app.UseApiKeyAuth();   // validates x-api-key on /v1/* routes

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("per-api-key");

app.Run();

// Make Program visible to integration test factory
public partial class Program { }
