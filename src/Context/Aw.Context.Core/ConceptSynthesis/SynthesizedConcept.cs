namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>A concept synthesized across sources, kept under a stable key.</summary>
/// <param name="Key">The concept's slug (for example 'checkout-retry'); a relabel keeps it.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">What the concept is, in a few sentences.</param>
/// <param name="Members">The records it groups.</param>
/// <param name="MembershipHash">A hash of the membership, so a pass can tell a changed group from an unchanged one.</param>
/// <param name="State">Whether the model has labeled this membership.</param>
/// <param name="Watermark">The embedding.seq high-water the row was computed at.</param>
/// <param name="CreatedAt">When the key first appeared; saves never move it.</param>
/// <param name="UpdatedAt">When the row last changed.</param>
public sealed record SynthesizedConcept(string Key, string Name, string Summary, IReadOnlyList<ConceptMember> Members, string MembershipHash, SynthesisState State, long Watermark, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
