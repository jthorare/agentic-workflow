using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>A state timeout (E-23).</summary>
/// <param name="Kind">What it does.</param>
/// <param name="InSeconds">Seconds left.</param>
/// <param name="On">What it is on.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusTimeout(
    TimeoutKind Kind,
    long InSeconds,
    string On);
