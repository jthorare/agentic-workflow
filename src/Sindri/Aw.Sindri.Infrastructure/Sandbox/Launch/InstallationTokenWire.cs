using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>GitHub's 201 answer to the installation-token request.</summary>
/// <param name="Token">The token.</param>
/// <param name="ExpiresAt">When it expires.</param>
internal sealed record InstallationTokenWire([property: JsonPropertyName("token")] string? Token, [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt);
