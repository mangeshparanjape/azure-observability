using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Payment.Orchestration.Services.HealthCheckAggregate.Dependencies;

namespace Payment.Orchestration.Services.HealthCheckAggregate;

/// <summary>Aggregates all enabled payment-processing dependency health checks.</summary>
/// <remarks>
/// Calls the local orchestration check without HTTP and passes PaymentRequestContext
/// explicitly to the cloud check. Returns Unhealthy if any enabled dependency is
/// unhealthy or degraded, or if no checks are enabled.
/// </remarks>
public sealed class HealthCheckAggregateService : IHealthCheckAggregateService
{
    private readonly OrchestrationHealthCheckService _orchestrationHealthCheckService;
    private readonly CloudPaymentHealthCheck _cloudPaymentHealthCheck;
    private readonly OnPremHealthCheck _onPremHealthCheck;
    private readonly ILogger<HealthCheckAggregateService> _logger;

    /// <summary>Creates the dependency aggregation service.</summary>
    /// <param name="orchestrationHealthCheckService">Existing local orchestration checker.</param>
    /// <param name="cloudPaymentHealthCheck">Cloud Payment API checker.</param>
    /// <param name="onPremHealthCheck">On-premises Payment API checker.</param>
    /// <param name="logger">Aggregation logger.</param>
    public HealthCheckAggregateService(
        OrchestrationHealthCheckService orchestrationHealthCheckService,
        CloudPaymentHealthCheck cloudPaymentHealthCheck,
        OnPremHealthCheck onPremHealthCheck,
        ILogger<HealthCheckAggregateService> logger)
    {
        _orchestrationHealthCheckService = orchestrationHealthCheckService;
        _cloudPaymentHealthCheck = cloudPaymentHealthCheck;
        _onPremHealthCheck = onPremHealthCheck;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthReport> CheckHealthAsync(
        PaymentRequestContext paymentContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentContext);
        var totalTimer = Stopwatch.StartNew();
        var checks = new Dictionary<string, Func<Task<HealthCheckResult>>>();

        // Registration-name identifiers are preserved, but the checks are now invoked
        // explicitly to allow passing request-specific Operation and Channel.
        if (OrchestrationConfiguration.AggregateHealthChecksOrchestratorEnabled)
        {
            checks.Add("paymentOrchestratorApi", () =>
                _orchestrationHealthCheckService.CheckHealthAsync(
                    new HealthCheckContext
                    {
                        Registration = new HealthCheckRegistration(
                            "paymentOrchestratorApi", _orchestrationHealthCheckService,
                            HealthStatus.Unhealthy, tags: null)
                    }, cancellationToken));
        }

        if (OrchestrationConfiguration.AggregateHealthChecksCloudPaymentApiEnabled)
        {
            checks.Add("cloudPaymentApi", () =>
                _cloudPaymentHealthCheck.CheckHealthAsync(paymentContext, cancellationToken));
        }

        if (OrchestrationConfiguration.AggregateHealthChecksOnPremPaymentApiEnabled)
        {
            checks.Add("onPremPaymentApi", () =>
                _onPremHealthCheck.CheckHealthAsync(
                    new HealthCheckContext
                    {
                        Registration = new HealthCheckRegistration(
                            "onPremPaymentApi", _onPremHealthCheck,
                            HealthStatus.Unhealthy, tags: null)
                    }, cancellationToken));
        }

        var tasks = checks.Select(async check =>
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var result = await check.Value();
                return (Name: check.Key, Result: result, Duration: timer.Elapsed);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for {Dependency}.", check.Key);
                return (Name: check.Key,
                    Result: HealthCheckResult.Unhealthy("Dependency health check failed."),
                    Duration: timer.Elapsed);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        var entries = results.ToDictionary(
            item => item.Name,
            item => new HealthReportEntry(
                item.Result.Status,
                item.Result.Description,
                item.Duration,
                item.Result.Exception,
                item.Result.Data));

        var isHealthy = entries.Count > 0 &&
            entries.Count == checks.Count &&
            entries.Values.All(x => x.Status == HealthStatus.Healthy);

        return new HealthReport(
            entries,
            isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            totalTimer.Elapsed);
    }
}
