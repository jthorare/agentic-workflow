namespace Aw.Judge.Core.Questions;

/// <summary>What happened to one candidate on this recipe step before: counts only.</summary>
/// <param name="Key">The candidate's key.</param>
/// <param name="Runs">How many runs.</param>
/// <param name="Refused">How many the model refused.</param>
/// <param name="CheckFailed">How many a check failed.</param>
/// <param name="Redraft">How many caused a redraft.</param>
public sealed record ModelPickHistory(string Key, int Runs, int Refused, int CheckFailed, int Redraft);
