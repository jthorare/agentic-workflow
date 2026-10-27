namespace Aw.Judge.Core.Questions;

/// <summary>For an escalation: the model that just failed and how.</summary>
/// <param name="Model">The model that failed.</param>
/// <param name="How">A code: refused or check-failed.</param>
/// <param name="Check">The failed check's name, or null.</param>
public sealed record ModelPickPrevious(string Model, string How, string? Check = null);
