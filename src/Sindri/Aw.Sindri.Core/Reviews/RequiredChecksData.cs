namespace Aw.Sindri.Core.Reviews;

/// <summary>The requiredChecks panel.</summary>
/// <param name="Checks">The checks that apply, in settings order.</param>
public sealed record RequiredChecksData(IReadOnlyList<ReviewPageCheck> Checks) : ReviewPanelData;
