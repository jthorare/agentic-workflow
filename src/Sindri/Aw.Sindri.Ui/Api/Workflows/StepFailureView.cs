namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>A failed node run in the words of Sindri's §6.1 table (#3304): the app shows these strings as served and never builds a sentence of its own.</summary>
/// <param name="Code">The failure code, such as "SND-CODE-004".</param>
/// <param name="Step">The failed step's title ("Check").</param>
/// <param name="Sentence">What happened, naming the step by its title.</param>
/// <param name="FixLabel">The fix button's words, or null when there is no fix.</param>
/// <param name="FixCommand">The command the fix runs, or null when it has none.</param>
/// <param name="SecondaryLabel">A second action's words, or null.</param>
/// <param name="Detail">The engine's own text, for Details.</param>
internal sealed record StepFailureView(string Code, string Step, string Sentence, string? FixLabel, string? FixCommand, string? SecondaryLabel, string Detail);
