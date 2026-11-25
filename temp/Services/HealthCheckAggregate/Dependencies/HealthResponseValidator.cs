using System.Net.Http.Json;
using System.Text.Json;
using Payment.Orchestration.Services.HealthCheckAggregate.Models;

namespace Payment.Orchestration.Services.HealthCheckAggregate.Dependencies;

/// <summary>
/// Validates HTTP health-check responses returned by payment dependencies.
/// </summary>
internal static class HealthResponseValidator
{
    /// <summary>
    /// Determines whether an HTTP response indicates a healthy dependency.
    /// </summary>
    /// <param name="response">The HTTP response returned by the dependency.</param>
    /// <param name="cancellationToken">Token used to cancel response processing.</param>
    /// <returns>
    /// <see langword="true"/> only when the HTTP request succeeds and its JSON
    /// response body explicitly reports a healthy status; otherwise, <see langword="false"/>.
    /// </returns>
    internal static async Task<bool> IsHealthyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        try
        {
            var healthResponse = await response.Content
                .ReadFromJsonAsync<AggregateHealthResponse>(cancellationToken);

            return healthResponse?.Healthy == true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
