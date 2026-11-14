namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>One row of the failure-to-sentence table (Workflows page spec §6.1): the code, the owner-facing sentence and the fix.</summary>
/// <param name="Code">The finding or error code, such as "SND-GRAPH-103" or "GRAPH-CONFIG-001".</param>
/// <param name="Template">The sentence with {step}-style placeholders.</param>
/// <param name="FixLabel">The button label (placeholders allowed), or null when the row has no fix.</param>
/// <param name="FixCommand">The command string a session runs, or null when the row has none.</param>
/// <param name="SecondaryLabel">A second button label, or null; only SND-GRAPH-103 has one ("Open {step}").</param>
public sealed record FailureSentence(string Code, string Template, string? FixLabel, string? FixCommand, string? SecondaryLabel = null);
