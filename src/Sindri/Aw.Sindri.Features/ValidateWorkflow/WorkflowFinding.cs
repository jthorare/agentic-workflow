namespace Aw.Sindri.Features.ValidateWorkflow;

/// <summary>One finding of the validator, as the API shows it.</summary>
/// <param name="Code">The finding's code, such as GRAPH-REF-001.</param>
/// <param name="Severity">'error' or 'warning'.</param>
/// <param name="Nodes">The nodes it names.</param>
/// <param name="Field">The config field it names, or null.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">How to fix it.</param>
internal sealed record WorkflowFinding(string Code, string Severity, IReadOnlyList<string> Nodes, string? Field, string Message, string Fix);
