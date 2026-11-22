namespace Aw.Sindri.Core.Graph.Failures;

/// <summary>A failed step, in the words of the §6.1 table (Workflows page spec §7.3): the sentence the run page shows, its fix, and the raw text for Details.</summary>
/// <param name="Code">The failure code, such as "SND-CODE-004".</param>
/// <param name="Sentence">The owner-facing sentence, naming the step by its title.</param>
/// <param name="FixLabel">The button label with the step's title filled in, or null when the row has no fix.</param>
/// <param name="FixCommand">The command a session runs, or null when the row has none.</param>
/// <param name="SecondaryLabel">A second button label, or null.</param>
/// <param name="Detail">The node run's error as the engine wrote it ("SND-CODE-004: InvalidOperationException: ..."), for Details.</param>
public sealed record StepFailure(string Code, string Sentence, string? FixLabel, string? FixCommand, string? SecondaryLabel, string Detail);
