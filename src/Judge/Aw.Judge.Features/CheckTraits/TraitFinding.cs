namespace Aw.Judge.Features.CheckTraits;

/// <summary>One trait a file breaks.</summary>
/// <param name="Trait">The trait's name.</param>
/// <param name="Severity">'block' (the Stop gate holds the agent back) or 'warn'.</param>
/// <param name="File">The file, relative to the repo.</param>
/// <param name="Probability">How likely the file breaks the trait, 0 to 1.</param>
/// <param name="Message">What to fix, one line.</param>
public sealed record TraitFinding(string Trait, string Severity, string File, double Probability, string Message);
