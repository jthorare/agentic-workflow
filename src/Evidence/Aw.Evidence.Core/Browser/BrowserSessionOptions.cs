using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Browser;

/// <summary>How to open a browser session. Navigation off the host's origin is always aborted.</summary>
/// <param name="Host">The vetted host; a preview host gets the launch arguments it needs.</param>
/// <param name="Viewport">The viewport (device scale factor 1).</param>
/// <param name="RecordDirectory">Where to record video and tracing; null records nothing.</param>
/// <param name="UserAgentOverride">A user agent to present instead of desktop Chrome (UI_EVIDENCE_USER_AGENT).</param>
/// <param name="LocalStorage">Entries written verbatim into the host origin's localStorage before every page load, so the app reads them on its first render and after each navigation; null writes none.</param>
public sealed record BrowserSessionOptions(
    ResolvedHost Host, ViewportSize Viewport, string? RecordDirectory, string? UserAgentOverride, IReadOnlyDictionary<string, string>? LocalStorage = null);
