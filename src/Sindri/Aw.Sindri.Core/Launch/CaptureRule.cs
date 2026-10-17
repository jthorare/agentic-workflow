using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The capture rule the launcher writes to <c lang="text">launches/&lt;id&gt;.capture.json</c> (0600, no secret; launcher L-9) for an entry whose CLI reports its own
/// session id: how 'session wrap' checks and, without the hook, finds that id.
/// </summary>
/// <param name="SessionIdPattern">The entry's pattern an id must match, or null.</param>
/// <param name="NewestFile">The glob of the CLI's newest-file fallback ('~' expands), or null when the CLI has none.</param>
/// <param name="Capture">The regex whose first group is the id in a file's path, or null.</param>
public sealed record CaptureRule(
    [property: JsonPropertyName("sessionIdPattern")] string? SessionIdPattern,
    [property: JsonPropertyName("newestFile")] string? NewestFile,
    [property: JsonPropertyName("capture")] string? Capture);
