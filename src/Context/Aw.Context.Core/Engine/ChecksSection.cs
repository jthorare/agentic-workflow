using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The profile's 'context.checks' as written: the mode and the limits of the map structure checks, every key optional, unknown keys refused.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksSection(string? Mode = null, ChecksIsolatedSection? Isolated = null, ChecksOverCapSection? OverCap = null);
