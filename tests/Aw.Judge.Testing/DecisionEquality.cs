using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Testing;

/// <summary>Value equality for decisions: the record's own equality compares its list and dictionary by reference.</summary>
public static class DecisionEquality
{
    /// <summary>Whether two decisions hold the same values, the detail row included.</summary>
    /// <param name="expected">The expected decision.</param>
    /// <param name="actual">The actual decision.</param>
    /// <returns>True when every field matches.</returns>
    public static bool Same(DecisionRecord expected, DecisionRecord actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        return expected.Skipped.SequenceEqual(actual.Skipped)
            && expected with { Skipped = [], Details = null } == actual with { Skipped = [], Details = null }
            && SameDetails(expected.Details, actual.Details);
    }

    private static bool SameDetails(DecisionDetails? expected, DecisionDetails? actual) =>
        expected is null || actual is null
            ? expected is null && actual is null
            : SameProbabilities(expected.Probabilities, actual.Probabilities)
                && (expected.Reasons is null ? actual.Reasons is null : actual.Reasons is not null && expected.Reasons.SequenceEqual(actual.Reasons, StringComparer.Ordinal))
                && expected with { Probabilities = null, Reasons = null } == actual with { Probabilities = null, Reasons = null };

    private static bool SameProbabilities(IReadOnlyDictionary<string, double>? expected, IReadOnlyDictionary<string, double>? actual) =>
        expected is null || actual is null
            ? expected is null && actual is null
            : expected.Count == actual.Count && expected.All(p => actual.TryGetValue(p.Key, out var value) && value.Equals(p.Value));
}
