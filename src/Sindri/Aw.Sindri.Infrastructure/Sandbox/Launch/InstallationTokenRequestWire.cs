using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The body of GitHub's installation-token request.</summary>
/// <param name="Repositories">The repositories the token is narrowed to.</param>
internal sealed record InstallationTokenRequestWire([property: JsonPropertyName("repositories")] string[] Repositories);
