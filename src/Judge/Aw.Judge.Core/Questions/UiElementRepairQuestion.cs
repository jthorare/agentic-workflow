using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// ui-element-repair: a Playwright step's selector broke; does one of the page's elements clearly match the step
/// ('repaired', with 'chosenIndex') or none ('no-good-candidate')? No candidates is 'no-good-candidate'; never guess.
/// </summary>
public static class UiElementRepairQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "ui-element-repair";

    /// <summary>One candidate clearly matches.</summary>
    public const string Repaired = "repaired";

    /// <summary>No candidate clearly matches.</summary>
    public const string NoGoodCandidate = "no-good-candidate";

    /// <summary>The extra response field: the chosen candidate's index.</summary>
    public const string ChosenIndex = "chosenIndex";

    /// <summary>The definition.</summary>
    public static QuestionDefinition<UiElementRepairInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [Repaired, NoGoodCandidate],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Repaired] = "one candidate element clearly matches the failed step's intent",
            [NoGoodCandidate] = "no candidate element clearly matches the step, so do not guess",
        },
        ContentClass = ContentClass.Code,
        Threshold = 0.6,
        TimeBudget = TimeSpan.FromSeconds(10),
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [ChosenIndex] = new() { ["type"] = "integer" } },
        Prompt = Prompt,
        PreRules = PreRule,
    };

    internal static string? PreRule(UiElementRepairInput input) => input.Candidates.Count == 0 ? NoGoodCandidate : null;

    private static string Prompt(UiElementRepairInput input) => string.Join('\n',
    [
        $"A Playwright step failed: \"{input.Step}\" could not find \"{input.BrokenSelector}\".",
        "Here are the other elements found on the page at that moment:",
        .. input.Candidates.Select(Describe),
        "Reply {\"decision\":\"repaired\",\"chosenIndex\":N} if one candidate clearly matches the step's intent, or {\"decision\":\"no-good-candidate\"} if none do. Never guess.",
    ]);

    private static string Describe(UiCandidate c) => string.Create(
        CultureInfo.InvariantCulture,
        $"[{c.Index}] role={c.Role ?? "?"} name={c.AccessibleName ?? "?"} testId={c.TestIdAttribute ?? "?"} text={c.Text ?? "?"}");
}
