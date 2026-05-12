# l402-example-aspnet

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**A live reference app for [Native L402 integration](https://docs.lightningenable.com/products/l402-microtransactions/native-integration) on ASP.NET Core (.NET 8).** Curl it from your terminal and watch a `402 Payment Required` come back with a real Lightning invoice.

The whole L402 integration is **2 lines + an attribute**.

## Try the live demo

> Live URL: *(deploy your own — see below)*

```bash
# Health check — free, ungated
curl -i $URL/api/free/health

# Premium endpoint — 100 sats per request
curl -i $URL/api/premium/weather?city=miami

# Variable pricing — premium model costs 500 sats
curl -i "$URL/api/premium/llm?prompt=hello&model=premium"

# OpenAPI spec (Lightning Enable's Scan API will import this for you)
curl $URL/swagger/v1/swagger.json
```

## Run locally

```bash
git clone https://github.com/refined-element/l402-example-aspnet
cd l402-example-aspnet

cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json, fill in LightningEnable:ApiKey
# (generate one at https://api.lightningenable.com/dashboard/settings)

dotnet run
```

Then in another terminal:

```bash
curl -i http://localhost:5000/api/premium/weather
```

You should see something like:

```http
HTTP/1.1 402 Payment Required
Content-Type: application/json
WWW-Authenticate: L402 macaroon="AgEL...", invoice="lnbc1u..."

{
  "error": "Payment Required",
  "l402": {
    "macaroon": "AgEL...",
    "invoice": "lnbc1u...",
    "amount_sats": 100,
    "payment_hash": "abc123...",
    "expires_at": "2026-05-12T01:00:00Z",
    "resource": "/api/premium/weather"
  }
}
```

Pay that invoice with any Lightning wallet to get a preimage, then retry:

```bash
curl -i http://localhost:5000/api/premium/weather \
  -H 'Authorization: L402 AgEL...:<your-preimage-hex>'
```

You'll get the real weather response.

## What's actually in the code

The whole L402 integration is one DI call, one middleware mount, and one attribute:

```csharp
// Program.cs
using L402Server.AspNetCore;          // ← 1. import

builder.Services.AddL402AspNetCore(opts =>     // ← 2. DI
{
    opts.ApiKey = builder.Configuration["LightningEnable:ApiKey"]!;
});

app.UseRouting();
app.UseL402();                                  // ← 3. mount the middleware

app.MapGet("/api/premium/weather", (HttpContext ctx, string? city) =>
    Results.Ok(new { city, temp = 72 }))
    .WithMetadata(new L402Attribute { PriceSats = 100 });   // ← 4. gate this endpoint
```

The rest is plain ASP.NET Core minimal API. See [`Program.cs`](./Program.cs) for the whole file (under 130 lines).

## Demonstrates

- **Free + paid routes in the same app** — `/api/free/health` passes through; `/api/premium/*` is gated
- **`[L402(PriceSats = N)]` attribute** for declarative per-route gating
- **Function-form variable pricing** via `opts.PriceSelector` — `?model=premium` costs 500 sats, default 100
- **OpenAPI spec exposed at `/swagger/v1/swagger.json`** — Lightning Enable's Scan API can import this directly to auto-populate the endpoint catalog in the merchant dashboard
- **`HttpContext.Items[L402HttpContextKeys.VerificationResult]` access** — handlers read the verified credential and echo it in the response

## Deploy your own

### Render (Docker, free tier)

1. Fork this repo
2. Visit https://render.com/deploy and connect your fork
3. Render reads `render.yaml`, builds the Docker image, provisions a free web service
4. Set `LightningEnable__ApiKey` in the Render dashboard env vars (note the double underscore — that's how .NET configuration maps to `LightningEnable:ApiKey`)

### fly.io

```bash
fly launch --copy-config --no-deploy
fly secrets set LightningEnable__ApiKey=<your-key>
fly deploy
```

### Azure Container Apps

```bash
az containerapp up \
  --name l402-example-aspnet \
  --resource-group <your-rg> \
  --location eastus \
  --source . \
  --env-vars LightningEnable__ApiKey=<your-key>
```

All three configs use the same `Dockerfile`.

## OpenAPI / Scan API story

This app exposes its endpoint catalog at `/swagger/v1/swagger.json`. Once deployed:

1. Go to **Lightning Enable Dashboard → Proxies → your proxy → Pricing tab**
2. Click **Scan API** and provide your deployed URL (the scanner probes well-known OpenAPI paths automatically)
3. The dashboard imports `/api/premium/weather` and `/api/premium/llm` as discovered endpoints with their per-route metadata

That's the path most merchants will use to populate their endpoint catalog without manually adding each one.

## Modify and play

```bash
dotnet watch run   # auto-reload on save
```

Things to try:
- Add a new paid endpoint with a different `[L402(PriceSats = X)]` value
- Replace the mock weather data with a real upstream API call
- Tighten the `PriceSelector` — by header, by user, by time of day
- Add `OnInvalidToken` to send a fresh 402 instead of 401

## Production checklist

When you graduate from this example to your real API:

- [ ] Generate a real `LIGHTNING_ENABLE_API_KEY` from your production merchant account
- [ ] Verify your payment provider (Strike or OpenNode) is configured in the Lightning Enable dashboard
- [ ] Confirm reverse proxy / load balancer forwards the `Authorization` header to your ASP.NET Core app
- [ ] Wire structured logging — `HttpContext.Items[L402HttpContextKeys.VerificationResult]` has `paymentHash`, `amountSats`, `resource` for usage analytics
- [ ] Consider `OnInvalidToken` if you want fresh-402 on retry instead of 401
- [ ] If you need strict single-use semantics, persist `paymentHash` to your DB on first successful verify and reject replays in your handler (the producer API allows token reuse within the validity window — see [docs](https://docs.lightningenable.com/products/l402-microtransactions/producer-api-reference#token-reuse-within-the-validity-window))

## Source

- This app: https://github.com/refined-element/l402-example-aspnet
- The middleware: https://github.com/refined-element/le-server-l402-aspnetcore-dotnet ([`L402Server.AspNetCore` on NuGet](https://www.nuget.org/packages/L402Server.AspNetCore))
- The SDK: https://github.com/refined-element/le-server-l402-dotnet ([`L402Server` on NuGet](https://www.nuget.org/packages/L402Server))
- Lightning Enable docs: https://docs.lightningenable.com/products/l402-microtransactions/native-integration

## License

MIT
