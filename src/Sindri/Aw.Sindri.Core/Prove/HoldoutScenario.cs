namespace Aw.Sindri.Core.Prove;

/// <summary>One scenario of a phase's hidden set (line spec §9.1): the text only the evaluator and the owner may read.</summary>
/// <param name="Key">The scenario's key, the "id" of set.json ('P1-H1').</param>
/// <param name="Item">The visible acceptance item it is tied to; null for the canary scenario.</param>
/// <param name="Kind">What runs it: api, ui, db, canary and so on.</param>
/// <param name="Steps">What the evaluator does.</param>
/// <param name="Expected">What should happen.</param>
/// <param name="Phrase">The canary scenario's token phrase; null on every other scenario.</param>
public sealed record HoldoutScenario(string Key, string? Item, string Kind, IReadOnlyList<string> Steps, string Expected, string? Phrase)
{
    /// <summary>The kind of the canary scenario.</summary>
    public const string CanaryKind = "canary";

    /// <summary>Whether this is the set's canary scenario.</summary>
    public bool IsCanary => string.Equals(Kind, CanaryKind, StringComparison.Ordinal);
}
