namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What a viewer did.</summary>
/// <param name="Kind">'warp', 'exec-template' or 'print'.</param>
/// <param name="Label">The label, or null.</param>
/// <param name="Outcome">'opened', 'unconfirmed', 'skipped' or 'failed'.</param>
/// <param name="Code">The failure code, or null.</param>
/// <param name="Detail">The failure sentence, or null.</param>
/// <param name="Reason">Why the viewer was skipped, as served, or null.</param>
internal sealed record LaunchViewerView(string Kind, string? Label, string Outcome, string? Code = null, string? Detail = null, string? Reason = null);
