using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>The newest launch that started.</summary>
/// <param name="Target">The agent.</param>
/// <param name="At">When the launch was requested.</param>
/// <param name="TabTitle">The viewer's tab title, or null.</param>
/// <param name="Viewer">What the viewer did, or null.</param>
/// <param name="Late">True when the viewer attached after the window.</param>
/// <param name="Attach">The attach command, or null.</param>
public sealed record LaunchLastStarted(AgentId Target, DateTimeOffset At, string? TabTitle, LaunchViewerPublic? Viewer, bool Late, IReadOnlyList<string>? Attach);
