namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflows/{slug}/pins: the output is pinned.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision it was pinned on.</param>
/// <param name="Node">The step.</param>
/// <param name="PinnedBy">Who pinned it.</param>
internal sealed record WorkflowPinned(string Slug, int Revision, string Node, string PinnedBy);
