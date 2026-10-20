using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>Something the design adds beyond what the scope stop approved (LC-7).</summary>
/// <param name="From">The station it escapes from.</param>
/// <param name="Station">The station that added it.</param>
/// <param name="Kind">acceptance or surface.</param>
/// <param name="Target">The item or surface.</param>
/// <param name="Why">Why.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopEscape(
    string From,
    string Station,
    string Kind,
    string Target,
    string Why);
