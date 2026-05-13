// l402-example-aspnet — reference app for Native L402 integration on ASP.NET Core.
//
// The entire L402 integration is the two using statements below + the three
// L402-related calls in the pipeline (AddL402AspNetCore, UseL402, and the
// L402Attribute metadata on the paid endpoints). Everything else is plain
// ASP.NET Core minimal API.

using L402Server;                  // VerificationResult type
using L402Server.AspNetCore;       // l402 middleware + [L402] attribute

var builder = WebApplication.CreateBuilder(args);

// DI wiring for the L402 SDK + middleware.
builder.Services.AddL402AspNetCore(opts =>
{
    opts.ApiKey = builder.Configuration["LightningEnable:ApiKey"]
        ?? throw new InvalidOperationException(
            "Missing LightningEnable:ApiKey. Set it via appsettings.Development.json " +
            "or the LightningEnable__ApiKey environment variable. Generate a key at " +
            "https://api.lightningenable.com/dashboard/settings.");
});

// OpenAPI / Swagger setup — exposes /swagger/v1/swagger.json which Lightning
// Enable's Scan API can import automatically to populate the endpoint catalog
// in the merchant dashboard.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "l402-example-aspnet",
        Version = "v1",
        Description =
            "Reference paid API gated by Lightning Enable's L402 middleware. " +
            "All endpoints under /api/premium require an L402 Lightning payment.",
    });
});

var app = builder.Build();

// Swagger UI in every environment so the deployed instance can be browsed
// by anyone curious — and so Lightning Enable's Scan API can find the spec
// at /swagger/v1/swagger.json.
app.UseSwagger();
app.UseSwaggerUI();

// L402 middleware — placed after routing so it can read [L402] attribute
// metadata from the matched endpoint. The PriceSelector only kicks in for
// /api/premium/* requests with ?model=premium to bump the price to 500 sats;
// for everything else it returns null so the gating falls back to the
// [L402] attribute (or no gating, if the route has no attribute).
app.UseRouting();
app.UseL402(opts =>
{
    opts.PriceSelector = ctx =>
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/premium/")) return ValueTask.FromResult<int?>(null);
        if (ctx.Request.Query["model"] == "premium") return ValueTask.FromResult<int?>(500);
        return ValueTask.FromResult<int?>(null);
    };
});

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------

// Free endpoint — ungated (no [L402] attribute, no DefaultPriceSats).
app.MapGet("/api/free/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTime.UtcNow,
}))
.WithName("Health")
.WithSummary("Health check (free, ungated)");

// Paid endpoint — 100 sats per request by default; 500 if ?model=premium.
app.MapGet("/api/premium/weather", (HttpContext ctx, string? city) =>
{
    var verified = ctx.Items[L402HttpContextKeys.VerificationResult] as VerificationResult;
    var conditions = new[] { "sunny", "cloudy", "partly cloudy", "windy" };
    return Results.Ok(new
    {
        city = city ?? "Miami",
        temperatureF = 72 + Random.Shared.Next(-10, 11),
        conditions = conditions[Random.Shared.Next(conditions.Length)],
        timestamp = DateTime.UtcNow,
        l402 = verified,
    });
})
.WithMetadata(new L402Attribute { PriceSats = 100, Description = "Premium weather forecast" })
.WithName("Weather")
.WithSummary("Mock weather data (100 sats per request, 500 with ?model=premium)");

// Paid endpoint — same pricing rules, demonstrates a second L402-gated route.
app.MapGet("/api/premium/llm", (HttpContext ctx, string? prompt, string? model) =>
{
    var verified = ctx.Items[L402HttpContextKeys.VerificationResult] as VerificationResult;
    var chosenModel = model ?? "standard";
    var actualPrompt = prompt ?? "Hello, world.";
    return Results.Ok(new
    {
        model = chosenModel,
        prompt = actualPrompt,
        completion = $"[mock {chosenModel} completion for: {actualPrompt}]",
        tokensUsed = 42,
        l402 = verified,
    });
})
.WithMetadata(new L402Attribute { PriceSats = 100, Description = "Mock LLM completion" })
.WithName("Llm")
.WithSummary("Mock LLM completion (100 sats standard, 500 with ?model=premium)");

// Root — explain what this is.
app.MapGet("/", () => Results.Text(
    """
    l402-example-aspnet — reference app for Native L402 integration

    Endpoints:
      GET /api/free/health                # free
      GET /api/premium/weather            # 100 sats (500 with ?model=premium)
      GET /api/premium/llm?prompt=hi      # 100 sats (500 with ?model=premium)

    OpenAPI spec:      GET /swagger/v1/swagger.json
    Interactive UI:    GET /swagger

    Source: https://github.com/refined-element/l402-example-aspnet
    """, contentType: "text/plain"));

app.Run();
