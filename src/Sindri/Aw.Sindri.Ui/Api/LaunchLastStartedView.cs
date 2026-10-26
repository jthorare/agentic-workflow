using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>The newest launch that started.</summary>
/// <param name="Target">The agent id.</param>
/// <param name="At">When it was requested.</param>
/// <param name="TabTitle">The viewer's tab title, or null.</param>
/// <param name="Viewer">What the viewer did, or null.</param>
/// <param name="Late">True when the viewer attached after the window.</param>
/// <param name="Attach">The attach command, or null.</param>
internal sealed record LaunchLastStartedView(string Target, DateTimeOffset At, string? TabTitle, LaunchViewerView? Viewer, bool Late, IReadOnlyList<string>? Attach);
