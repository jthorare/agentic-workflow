namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>What a pass does with one group.</summary>
/// <param name="Group">The group.</param>
/// <param name="Existing">The stored concept the group continues, or null for a new concept.</param>
public sealed record ConceptPlan(ConceptGroup Group, SynthesizedConcept? Existing)
{
    /// <summary>The stored concept already names this exact membership: no model call.</summary>
    public bool Unchanged => Existing is { State: SynthesisState.Done } done && string.Equals(done.MembershipHash, Group.MembershipHash, StringComparison.Ordinal);
}
