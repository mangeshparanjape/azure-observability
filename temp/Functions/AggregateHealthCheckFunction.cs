using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Payment.Orchestration.Services.HealthCheckAggregate;
using Payment.Orchestration.Services.HealthCheckAggregate.Models;

namespace Payment.Orchestration.Functions;

/// <summary>Provides a client-facing health endpoint for all enabled payment dependencies.</summary>
public sealed class AggregateHealthCheckFunction
{
    private readonly IHealthCheckAggregateService _aggregateHealthCheckService;
    private readonly ILogger<AggregateHealthCheckFunction> _logger;

    /// <summary>Creates the aggregate health HTTP function.</summary>
    /// <param name="aggregateHealthCheckService">Service that checks enabled dependencies.</param>
    /// <param name="logger">Function logger.</param>
    public AggregateHealthCheckFunction(
        IHealthCheckAggregateService aggregateHealthCheckService,
        ILogger<AggregateHealthCheckFunction> logger)
    {
        _aggregateHealthCheckService = aggregateHealthCheckService;
        _logger = logger;
    }

    /// <summary>Returns strict combined health with the existing client-safe JSON shape.</summary>
    /// <param name="request">Incoming request, from which the existing payment context must be resolved.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>HTTP 200 when healthy, otherwise HTTP 503.</returns>
    [Function("aggregateHealthCheck")]
    [OpenApiOperation(operationId: "aggregateHealthCheck", tags: new[] { "HealthCheck" })]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(AggregateHealthResponse))]
    [OpenApiResponseWithBody(HttpStatusCode.ServiceUnavailable, "application/json", typeof(AggregateHealthResponse))]
    public async Task<ActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1.0/health/aggregate")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Resolve Operation and Channel from your existing client request contract.
            // Avoid accepting arbitrary unvalidated URLs from the caller.
            // Replace the following placeholder with the existing context factory/parser:
            // PaymentRequestContext paymentContext = ...;
            // var report = await _aggregateHealthCheckService.CheckHealthAsync(paymentContext, cancellationToken);
            // return MapReport(report);

            return new ObjectResult(new AggregateHealthResponse
            {
                Healthy = false,
                Description = "Aggregate health request context is not yet implemented."
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aggregate health check failed.");
            return new ObjectResult(new AggregateHealthResponse
            {
                Healthy = false,
                Description = "Payment health check failed."
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
    }

    /// <summary>Maps an internal health report to the safe public response.</summary>
    /// <param name="report">Internal health report.</param>
    /// <returns>HTTP result with the existing four-field response contract.</returns>
    private static ActionResult MapReport(HealthReport report)
    {
        var healthy = report.Status == HealthStatus.Healthy;
        var response = new AggregateHealthResponse
        {
            Healthy = healthy,
            Description = healthy
                ? "All enabled payment dependencies are healthy."
                : "One or more payment dependencies are unhealthy."
        };
        return new ObjectResult(response)
        {
            StatusCode = healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable
        };
    }
}
