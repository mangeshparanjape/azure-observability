using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Payment.Orchestration.Services.HealthCheckAggregate;

/// <summary>
/// Evaluates enabled orchestration, cloud-payment, and on-premises payment dependencies.
/// </summary>
public interface IHealthCheckAggregateService
{
    /// <summary>Runs checks for the requested payment operation and channel.</summary>
    /// <param name="paymentContext">Existing request context containing Operation and Channel.</param>
    /// <param name="cancellationToken">Cancels the health-check execution.</param>
    /// <returns>A report with individual dependency entries and a strict aggregate status.</returns>
    Task<HealthReport> CheckHealthAsync(
        PaymentRequestContext paymentContext,
        CancellationToken cancellationToken = default);
}
