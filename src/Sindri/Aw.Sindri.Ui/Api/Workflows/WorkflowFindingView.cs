namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One finding of the activation checks, as POST /sindri/workflows/validate serves it.</summary>
/// <param name="Code">The finding's code, such as GRAPH-REF-001.</param>
/// <param name="Severity">error or warning.</param>
/// <param name="Nodes">The nodes it names.</param>
/// <param name="Field">The config field it names, or null.</param>
/// <param name="Message">What is wrong (the same text as <paramref name="Cause"/>).</param>
/// <param name="Fix">How to fix it.</param>
/// <param name="Id">A stable id for the finding, so the page keeps its place across saves.</param>
/// <param name="Node">The node it belongs to, or null for the whole graph.</param>
/// <param name="Cause">What is wrong, in one line.</param>
internal sealed record WorkflowFindingView(string Code, string Severity, IReadOnlyList<string> Nodes, string? Field, string Message, string Fix, string Id, string? Node, string Cause);
