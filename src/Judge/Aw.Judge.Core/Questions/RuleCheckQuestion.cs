using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>rule-check: does a diff hunk break a rule ('violated'), follow it ('fine'), or is the rule beside the point ('n/a')? A blank hunk is 'n/a'.</summary>
public static class RuleCheckQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "rule-check";

    /// <summary>The hunk breaks the rule.</summary>
    public const string Violated = "violated";

    /// <summary>The hunk follows the rule.</summary>
    public const string Fine = "fine";

    /// <summary>The rule doesn't apply to the hunk.</summary>
    public const string NotApplicable = "n/a";

    /// <summary>The definition.</summary>
    public static QuestionDefinition<RuleCheckInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [Violated, Fine, NotApplicable],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Violated] = "the diff hunk breaks the rule",
            [Fine] = "the diff hunk follows the rule",
            [NotApplicable] = "the rule does not apply to this hunk at all",
        },
        ContentClass = ContentClass.Diff,
        Threshold = 0.6,
        TimeBudget = TimeSpan.FromSeconds(10),
        Prompt = Prompt,
        PreRules = PreRule,
    };

    internal static string? PreRule(RuleCheckInput input) => string.IsNullOrWhiteSpace(input.Hunk) ? NotApplicable : null;

    private static string Prompt(RuleCheckInput input) => string.Join('\n',
    [
        $"Rule: {input.Rule}",
        "Diff hunk:",
        input.Hunk,
        "Reply {\"decision\":\"violated\"} if this hunk breaks the rule, {\"decision\":\"fine\"} if it doesn't, or {\"decision\":\"n/a\"} if the rule doesn't apply to this hunk at all.",
    ]);
}
