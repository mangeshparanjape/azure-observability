# Payment orchestration — aggregated health check artifacts

These are consolidated source drafts based on our chat, **not** copies of your Visual Studio solution. Merge rather than overwrite existing project files.

## Included

- `Services/HealthCheckAggregate/IHealthCheckAggregateService.cs`
- `Services/HealthCheckAggregate/HealthCheckAggregateService.cs`
- `Services/HealthCheckAggregate/Dependencies/CloudPaymentHealthCheck.cs`
- `Services/HealthCheckAggregate/Dependencies/OnPremHealthCheck.cs`
- `Services/HealthCheckAggregate/Dependencies/HealthResponseValidator.cs`
- `Services/HealthCheckAggregate/Models/AggregateHealthResponse.cs`
- `Functions/AggregateHealthCheckFunction.cs`
- `Program.Registrations.example.cs.txt`

## Required integration before use

1. **Adapt namespaces and imports** to your actual solution. The code assumes your existing `PaymentRequestContext`, `OrchestrationConfiguration`, and `OrchestrationHealthCheckService` types are in scope; add the correct `using` directives. **Do not create duplicate versions of these types**.
2. The function intentionally returns **503** until you replace its TODO with your real mechanism for acquiring `PaymentRequestContext` (Operation, Channel) and call `CheckHealthAsync`. Then call `MapReport(report)`.
3. The Cloud GET route `/health/{operation}/{channel}` is **illustrative, not a confirmed endpoint**. Match your actual API route and auth contract; validate allowed operation/channel combinations.
4. The On-Prem POST request body and authentication remain TODOs until you match its existing API contract. Do not use a real transaction.
5. Both downstream endpoints are currently assumed to return an HTTP 2xx with JSON `{ "healthy": true }`. Verify the actual cloud and on-prem response formats (including JSON double-serialization).
6. The `AggregateHealthResponse` public model intentionally has only four fields with empty hostname/instanceId. Individual results remain inside the internal `HealthReport`.
7. Enabled flags are read from your existing static `OrchestrationConfiguration`; ensure App Configuration loads and refreshes them correctly. Validate that expected dependencies are not silently missing.
8. Match the external contract for HTTP status: the sample returns **503** when unhealthy; confirm whether the client requires HTTP 200 with `healthy:false` instead.
9. The sample assumes authorization is managed by your APIM design; validate `AuthorizationLevel.Anonymous` for your environment. Add tests, including timeouts, invalid JSON, disabled checks, missing config, cancellations and healthy/unhealthy aggregation.

DocFX XML documentation comments are included in the C# source.
