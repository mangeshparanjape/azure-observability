using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Payment.Orchestration.Services.HealthCheckAggregate.Dependencies;

/// <summary>Checks the Cloud Payment API for the specified operation and channel.</summary>
/// <remarks>
/// Invoked with the existing PaymentRequestContext by the aggregate service.
/// The framework IHealthCheck method cannot receive per-request payment context.
/// </remarks>
public sealed class CloudPaymentHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudPaymentHealthCheck> _logger;

    /// <summary>Creates a cloud payment dependency checker.</summary>
    /// <param name="httpClientFactory">Factory for the named CloudPaymentHealth client.</param>
    /// <param name="logger">Health-check logger.</param>
    public CloudPaymentHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<CloudPaymentHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Unhealthy(
            "PaymentRequestContext is required for the Cloud Payment API health check."));

    /// <summary>Checks cloud health for a payment operation and channel.</summary>
    /// <param name="paymentContext">Context containing Operation and Channel.</param>
    /// <param name="cancellationToken">Cancels the HTTP health-check request.</param>
    /// <returns>Cloud dependency health and response metrics.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        PaymentRequestContext paymentContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentContext);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var baseUrl = OrchestrationConfiguration.CloudPaymentHealthCheckUrl;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps)
            {
                return HealthCheckResult.Unhealthy("Cloud health URL is missing or invalid.");
            }

            // TODO: Replace this illustrative path with the actual Cloud Payment API route contract.
            // The URL must be derived from validated operation/channel values and a trusted base URL.
            var operation = Uri.EscapeDataString(paymentContext.Operation.ToString()!);
            var channel = Uri.EscapeDataString(paymentContext.Channel.ToString()!);
            var url = $"{baseUri.AbsoluteUri.TrimEnd('/')}/health/{operation}/{channel}";

            var client = _httpClientFactory.CreateClient("CloudPaymentHealth");
            using var response = await client.GetAsync(url, cancellationToken);
            var isHealthy = await HealthResponseValidator.IsHealthyAsync(response, cancellationToken);

            return new HealthCheckResult(
                isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                isHealthy ? "Cloud Payment API is healthy." : "Cloud Payment API is unhealthy.",
                data: new Dictionary<string, object>
                {
                    ["httpStatus"] = (int)response.StatusCode,
                    ["responseTimeMs"] = stopwatch.ElapsedMilliseconds
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _logger.LogWarning(ex, "Cloud Payment API health check failed.");
            return HealthCheckResult.Unhealthy(
                "Cloud Payment API health check failed.",
                data: new Dictionary<string, object> { ["responseTimeMs"] = stopwatch.ElapsedMilliseconds });
        }
    }
}
