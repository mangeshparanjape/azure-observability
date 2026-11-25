using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Payment.Orchestration.Services.HealthCheckAggregate.Dependencies;

/// <summary>Checks the on-premises payment health endpoint using HTTP POST.</summary>
/// <remarks>The on-premises health POST must be non-transactional.</remarks>
public sealed class OnPremHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OnPremHealthCheck> _logger;

    /// <summary>Creates an on-premises dependency checker.</summary>
    /// <param name="httpClientFactory">Factory for the named OnPremHealth client.</param>
    /// <param name="logger">Health-check logger.</param>
    public OnPremHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<OnPremHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = OrchestrationConfiguration.OnPremHealthCheckUrl;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                return HealthCheckResult.Unhealthy("On-premises health URL is missing or invalid.");
            }

            var client = _httpClientFactory.CreateClient("OnPremHealth");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            // TODO: Add required non-transactional POST body and authentication, if any.
            using var response = await client.SendAsync(request, cancellationToken);
            var isHealthy = await HealthResponseValidator.IsHealthyAsync(response, cancellationToken);

            return new HealthCheckResult(
                isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                isHealthy ? "On-premises Payment API is healthy." : "On-premises Payment API is unhealthy.",
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
            _logger.LogWarning(ex, "On-premises Payment API health check failed.");
            return HealthCheckResult.Unhealthy(
                "On-premises Payment API health check failed.",
                data: new Dictionary<string, object> { ["responseTimeMs"] = stopwatch.ElapsedMilliseconds });
        }
    }
}
