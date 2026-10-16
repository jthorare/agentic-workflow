using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A suspension's resume action (spec §5.8): what the answer that ends it runs.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Withheld">The withheld trigger, for transition, reevaluate, override and a drift rearm parked by a Step trigger.</param>
/// <param name="Gate">The escalated gate that '--accept' overrides.</param>
/// <param name="Effects">The refused effects that 'reemit' runs again under their identities (§5.7), and that a drift approval's resume re-sends (P2); a later refusal into the same park joins them.</param>
public sealed record ResumeAction(ResumeKind Kind, WithheldTrigger? Withheld, BlockId? Gate, IReadOnlyList<RefusedEffect> Effects)
{
    /// <summary>Equal when every part is, the effects by value in order (a list compares by reference otherwise).</summary>
    /// <param name="other">The other action.</param>
    /// <returns>Whether they are equal.</returns>
    public bool Equals(ResumeAction? other) =>
        other is not null && Kind == other.Kind && Equals(Withheld, other.Withheld) && Gate == other.Gate && Effects.SequenceEqual(other.Effects);

    /// <summary>The hash of the parts <see cref="Equals(ResumeAction?)"/> compares.</summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => HashCode.Combine(Kind, Withheld, Gate, Effects.Count);
}
