using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A proof signal.</summary>
/// <param name="Signal">The signal.</param>
/// <param name="Kind">Its evidence kind.</param>
/// <param name="RunsAt">head or base+head.</param>
/// <param name="Statement">What is observed.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopSignal(
    string Signal,
    string Kind,
    string RunsAt,
    string Statement);
