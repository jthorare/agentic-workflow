using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/launches/{id} (API P10a): one launch, as far as the app shows it. Fields the app does not use are left out.</summary>
/// <param name="Id">The launch id (LCH-…).</param>
/// <param name="Item">The item it belongs to.</param>
/// <param name="Mode">'open', 'attach' or 'resume'.</param>
/// <param name="State">'requested', 'started' or 'failed'.</param>
/// <param name="Target">The agent id.</param>
/// <param name="Code">The failure code, or null.</param>
/// <param name="Detail">The failure sentence with its fix, or null.</param>
/// <param name="Stage">Where a failure happened ('host'), or null.</param>
/// <param name="SessionId">The session started, or null.</param>
/// <param name="TabTitle">The viewer's tab title, or null.</param>
/// <param name="Viewer">What the viewer did, or null.</param>
/// <param name="Late">True when the session came up after the launch had been given up on.</param>
/// <param name="Attach">The attach command (print viewer), or null.</param>
/// <param name="AsOf">How fresh the view is.</param>
internal sealed record LaunchView(
    string Id,
    string Item,
    string Mode,
    string State,
    string Target,
    string? Code,
    string? Detail,
    string? Stage,
    string? SessionId,
    string? TabTitle,
    LaunchViewerView? Viewer,
    bool Late,
    IReadOnlyList<string>? Attach,
    AsOfView AsOf);
