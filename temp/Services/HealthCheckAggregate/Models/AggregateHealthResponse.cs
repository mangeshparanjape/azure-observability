using System.Text.Json.Serialization;

namespace Payment.Orchestration.Services.HealthCheckAggregate.Models;

/// <summary>
/// Represents the public aggregate health response for payment processing dependencies.
/// </summary>
/// <remarks>
/// Preserves the existing client contract. Internal host and instance identifiers
/// are intentionally not disclosed.
/// </remarks>
public sealed class AggregateHealthResponse
{
    /// <summary>Gets or sets whether every enabled dependency is healthy.</summary>
    [JsonPropertyName("healthy")]
    public bool Healthy { get; set; }

    /// <summary>Gets or sets a client-safe description of aggregate health.</summary>
    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets the intentionally empty hostname field required by the contract.</summary>
    [JsonPropertyName("hostname")]
    public string Hostname => string.Empty;

    /// <summary>Gets the intentionally empty instance identifier field required by the contract.</summary>
    [JsonPropertyName("instanceId")]
    public string InstanceId => string.Empty;
}
