using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentScope.Domain.Entities;
using AgentScope.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentScope.Api.Tests;

/// <summary>
/// Integration tests for POST /v1/traces.
/// Uses an in-memory SQLite database to avoid needing PostgreSQL.
/// </summary>
public sealed class TraceIngestionTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private string _apiKey = "";

    public TraceIngestionTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        // Ensure DB schema exists and seed a test application
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentScopeDbContext>();
        await db.Database.EnsureCreatedAsync();

        _apiKey = Guid.NewGuid().ToString("N");
        var user = User.Create($"{_apiKey}@test.com", "Test User");
        var app  = Application.Create(user.Id, $"App-{_apiKey}", _apiKey);
        await db.Users.AddAsync(user);
        await db.Applications.AddAsync(app);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task POST_traces_without_api_key_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/traces", new { resourceSpans = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_traces_with_invalid_api_key_returns_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", "this-key-does-not-exist-at-all");
        var response = await client.PostAsJsonAsync("/v1/traces", new { resourceSpans = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_traces_with_valid_key_and_empty_payload_returns_200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

        var response = await client.PostAsJsonAsync("/v1/traces",
            new { resourceSpans = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tracesIngested").GetInt32().Should().Be(0);
        body.GetProperty("spansIngested").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task POST_traces_with_valid_otlp_payload_persists_trace_and_spans()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

        var startNs = (DateTimeOffset.UtcNow.AddSeconds(-2).ToUnixTimeMilliseconds() * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var endNs   = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var payload = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new { attributes = Array.Empty<object>() },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = "agentscope-sdk", version = "1.0.0" },
                            spans = new[]
                            {
                                new
                                {
                                    traceId           = "0102030405060708090a0b0c0d0e0f10",
                                    spanId            = "0102030405060708",
                                    parentSpanId      = (string?)null,
                                    name              = "agent.run",
                                    kind              = 1,
                                    startTimeUnixNano = startNs,
                                    endTimeUnixNano   = endNs,
                                    attributes        = new[]
                                    {
                                        new { key = "gen_ai.system", value = new { stringValue = "monadic-sharp" } },
                                    },
                                    status = new { code = 1, message = "" },
                                },
                            },
                        },
                    },
                },
            },
        };

        var response = await client.PostAsJsonAsync("/v1/traces", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tracesIngested").GetInt32().Should().Be(1);
        body.GetProperty("spansIngested").GetInt32().Should().Be(1);

        // Verify DB has the trace
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AgentScopeDbContext>();
        var trace = await db.Traces
            .Include(t => t.Spans)
            .FirstOrDefaultAsync(t => t.TraceId == "0102030405060708090a0b0c0d0e0f10");

        trace.Should().NotBeNull();
        trace!.Spans.Should().HaveCount(1);
        trace.Spans.First().Name.Should().Be("agent.run");
    }
}

/// <summary>
/// WebApplicationFactory that replaces PostgreSQL with an in-memory SQLite
/// so the tests run without any external infrastructure.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    // Keep the SQLite connection alive for the lifetime of the factory
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestWebAppFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real PostgreSQL DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AgentScopeDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Replace with SQLite using the shared connection (keeps in-memory DB alive)
            services.AddDbContext<AgentScopeDbContext>(options =>
                options.UseSqlite(_connection));
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
