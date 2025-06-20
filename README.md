# Currency Converter API
A robust, scalable, and secure currency conversion API.

---

## Features

- Currency conversion using the [Frankfurter API](https://www.frankfurter.app/)
- Convert, Fetch Latest, and Historical exchange rates (with pagination)
- Disallowed currencies: `TRY`, `PLN`, `THB`, `MXN`
- Polly-based retry, exponential backoff & circuit breaker
- In-memory caching for performance
- Serilog with Seq for structured logging
- Rate limiting and API versioning
- Test coverage

---

## Getting Started

### Prerequisites
- .NET 8 SDK
- Seq (optional) for log visualization

### Setup Instructions
```bash
# Clone the repo
$ git clone https://github.com/mbeelalahmed/CurrencyConverterAPI.git
$ cd CurrencyConverterAPI

# Restore packages
$ dotnet restore

# Run the app
$ dotnet run --project src/CurrencyConverter.API

# Run tests
$ dotnet test
```

---

## Security & Access:
- **JWT Authentication** with `dummy-key` in `Appsettings`
- **RBAC:**
  - `/api/v1/exchange/historical` and `/convert` → `User`, `Admin` only
  - `/latest` → `Admin` & `operator`

  - There are two users of the system
  - Admin -> username (admin), password (password) - Full API permissions           
  - Operator -> username (operator), password (password) - Limited permissions

---

## Monitoring:
- **Logging:** Serilog + Console + Seq (port 5341)
- **Rate Limiting:** Fixed window (50 reqs/min)
- **Future Enhancement:** Add support for open telemetry, sample code is available in ```Progam.cs```

---

## Example API Calls
```http
GET /api/v1/exchange/latest?baseCurrency=EUR&provider=frankfurter
GET /api/v1/exchange/convert?from=EUR&to=USD&amount=100
GET /api/v1/exchange/historical?baseCurrency=EUR&start=2024-01-01&end=2024-01-10&page=1&size=5
```

---

## Environment Configuration
- Supports `Development`, `Test`, `Production`
- Environment-specific settings via:
  - `appsettings.{Environment}.json`
  - `launchSettings.json`

---

## API Versioning
- Enabled using `AddApiVersioning()` in `Program.cs`
- Current version: `v1`
- Future support for `v2`, etc.

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
});
```

---

## Deploy to Cluster
```bash
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
```
---

## Assumptions
- No support for TRY, PLN, THB, and MXN
- Frankfurter is the only provider for now (more can be added via factory)
- No persistence layer required, all data fetched via external API
- Only Valid input is passed to APIs

## Scalability
- Docker-ready with `Dockerfile`
- Kubernetes-ready with deployment & service manifests
- Stateless API allows easy horizontal scaling
- In-memory cache used, ready to switch to `IDistributedCache` (e.g., Redis)
