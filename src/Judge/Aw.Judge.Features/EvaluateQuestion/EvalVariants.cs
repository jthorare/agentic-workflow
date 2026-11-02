using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>
/// The input variants of a question: each is a pure transform of the stored input (more or less context for the same
/// labelled question). 'as-is' is always there.
/// </summary>
internal static class EvalVariants
{
    internal const string AsIs = "as-is";

    /// <summary>The transform of a variant, or null when the question has no such variant.</summary>
    internal static Func<JsonNode?, JsonNode?>? Find(string question, string variant) => (question, variant) switch
    {
        (_, AsIs) => static input => input,
        (ResolutionCheckQuestion.Name, "brief-only") => static input => With(input, ("rootCause", ""), ("diffStat", ""), ("diff", null)),
        (ResolutionCheckQuestion.Name, "brief+cause") => static input => With(input, ("diffStat", ""), ("diff", null)),
        (ResolutionCheckQuestion.Name, "stat") => static input => With(input, ("diff", null)),
        (ResolutionCheckQuestion.Name, "full") => static input => With(input),
        (TurnProgressQuestion.Name, "diff-only") => static input => With(input, ("problem", "(not given)"), ("acceptanceCriteria", "")),
        (TurnProgressQuestion.Name, "problem+diff") => static input => With(input, ("acceptanceCriteria", ""), ("priorDiffStat", ""), ("signals", "")),
        (TurnProgressQuestion.Name, "full") => static input => With(input),
        _ => null,
    };

    /// <summary>
    /// The full-versus-stat comparison must score the same items, so both variants run only over items that stored a diff
    /// (the others are left out, not counted as invalid input in one variant only).
    /// </summary>
    internal static bool NeedsStoredDiff(string question, string variant) =>
        string.Equals(question, ResolutionCheckQuestion.Name, StringComparison.Ordinal)
        && (string.Equals(variant, "stat", StringComparison.Ordinal) || string.Equals(variant, "full", StringComparison.Ordinal));

    internal static bool HasStoredDiff(JsonNode? input) =>
        input is JsonObject obj && obj["diff"] is JsonValue value && value.GetValueKind() == JsonValueKind.String;

    // A copy of the input as an object (anything else becomes an empty one), with each field set, or removed when null.
    private static JsonObject With(JsonNode? input, params (string Name, string? Value)[] fields)
    {
        var copy = input is JsonObject obj ? (JsonObject)obj.DeepClone() : [];
        foreach (var (name, value) in fields)
        {
            if (value is null)
            {
                _ = copy.Remove(name);
            }
            else
            {
                copy[name] = value;
            }
        }

        return copy;
    }
}
