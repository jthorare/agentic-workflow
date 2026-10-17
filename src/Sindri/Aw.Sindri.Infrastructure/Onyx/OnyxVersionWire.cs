using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>Onyx's `GET /api/version` reply (VersionResponse); only the field Sindri reads.</summary>
/// <param name="BackendVersion">The release the backend runs.</param>
internal sealed record OnyxVersionWire([property: JsonPropertyName("backend_version")] string? BackendVersion);
