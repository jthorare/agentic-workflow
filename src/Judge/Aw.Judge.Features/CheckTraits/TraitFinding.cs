namespace Aw.Judge.Features.CheckTraits;

/// <summary>One trait a file breaks.</summary>
/// <param name="Trait">The trait's name.</param>
/// <param name="Severity">'block' (the Stop gate holds the agent back) or 'warn'.</param>
/// <param name="File">The file, relative to the repo.</param>
/// <param name="Probability">How likely the file breaks the trait, 0 to 1.</param>
/// <param name="Message">What the rule requires, as the trait file says it.</param>
/// <param name="StartLine">The first line of the judged excerpt, or 0 when unknown.</param>
/// <param name="EndLine">The last line of the judged excerpt, or 0 when unknown.</param>
/// <param name="Guidance">How to fix it, when the trait file says.</param>
/// <param name="Details">The trait file, relative to the repo when it lies inside it.</param>
public sealed record TraitFinding(
    string Trait,
    string Severity,
    string File,
    double Probability,
    string Message,
    int StartLine = 0,
    int EndLine = 0,
    string? Guidance = null,
    string? Details = null);
