using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

// ── Config ────────────────────────────────────────────────────────────────────
var ApiEndpoint = Environment.GetEnvironmentVariable("AGENTSCOPE_ENDPOINT") ?? "http://localhost:5100";
var ApiKey = Environment.GetEnvironmentVariable("AGENTSCOPE_API_KEY") ?? throw new InvalidOperationException("AGENTSCOPE_API_KEY env var not set");
const string SourceName  = "AgentScope.SmokeTest";

// ── Trace provider (no DI needed in console) ──────────────────────────────────
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(SourceName)
    .AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = new Uri($"{ApiEndpoint}/v1/traces");
        otlp.Headers  = $"x-api-key={ApiKey}";
    })
    .Build();

var source = new ActivitySource(SourceName);
var rng    = new Random(42);

// ── Scenarios: (pipeline, LLM span, error message or null) ───────────────────
(string Pipeline, string LlmModel, string? Error)[] scenarios =
[
    ("InvoiceExtractionPipeline",  "gpt-4o",          null),
    ("CustomerQueryAgent",         "gpt-4o-mini",      null),
    ("SummaryGenerationChain",     "claude-3-5-haiku", null),
    ("RAGRetrievalAgent",          "gpt-4o",           "Result.Failure: upstream timeout"),
    ("ClassificationPipeline",     "gpt-4o-mini",      null),
    ("ContractAnalysisPipeline",   "gpt-4o",           "HttpRequestException: 503 Service Unavailable"),
    ("EmailDraftingAgent",         "claude-3-5-haiku", null),
    ("DataValidationChain",        "gpt-4o-mini",      "Result.Failure: tool not found"),
    ("CustomerQueryAgent",         "gpt-4o",           "Option.None: context missing"),
    ("SummaryGenerationChain",     "claude-3-5-haiku", "DomainError: rate limit exceeded"),
    ("InvoiceExtractionPipeline",  "gpt-4o",           null),
    ("RAGRetrievalAgent",          "gpt-4o",           "JsonException: unexpected token in response"),
];

Console.WriteLine($"Sending {scenarios.Length} traces to {ApiEndpoint}...\n");

foreach (var (pipeline, model, error) in scenarios)
{
    using var root = source.StartActivity(pipeline, ActivityKind.Server);
    if (root is null) { Console.WriteLine("[WARN] No listener — SDK not initialized"); break; }

    root.SetTag("service.name",   SourceName);
    root.SetTag("pipeline.name",  pipeline);

    // ── LLM span ────────────────────────────────────────────────────────────
    using (var llm = source.StartActivity("llm.completion", ActivityKind.Client))
    {
        llm?.SetTag("model",         model);
        llm?.SetTag("input_tokens",  rng.Next(200, 2000));
        llm?.SetTag("output_tokens", rng.Next(50, 500));
        await Task.Delay(rng.Next(80, 600));
    }

    // ── Retriever span (only for RAG pipelines) ──────────────────────────────
    if (pipeline.Contains("RAG") || pipeline.Contains("Query"))
    {
        using var retriever = source.StartActivity("retriever.search", ActivityKind.Internal);
        retriever?.SetTag("index", "documents-v2");
        retriever?.SetTag("top_k", 5);
        await Task.Delay(rng.Next(20, 150));
    }

    // ── Tool span (only for agents with tools) ───────────────────────────────
    if (pipeline.Contains("Agent") || pipeline.Contains("Validation"))
    {
        using var tool = source.StartActivity("tool.execute", ActivityKind.Internal);
        tool?.SetTag("tool.name", pipeline.Contains("Validation") ? "schema_validator" : "web_search");
        await Task.Delay(rng.Next(10, 80));
    }

    // ── Mark root span status ────────────────────────────────────────────────
    if (error is not null)
        root.SetStatus(ActivityStatusCode.Error, error);

    var status = error is null ? "✓" : "✗";
    Console.WriteLine($"  {status} {pipeline,-32} {(error is null ? "OK" : error)}");

    await Task.Delay(50); // small gap between traces
}

// Flush — give OTLP exporter time to ship
await Task.Delay(2000);
Console.WriteLine($"\nDone. Open http://localhost:5101 and check the dashboard.");
