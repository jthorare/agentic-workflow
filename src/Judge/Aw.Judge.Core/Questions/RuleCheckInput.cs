namespace Aw.Judge.Core.Questions;

/// <summary>rule-check's input: one rule and one diff hunk.</summary>
/// <param name="Rule">The rule's text.</param>
/// <param name="Hunk">The diff hunk.</param>
public sealed record RuleCheckInput(string Rule, string Hunk);
