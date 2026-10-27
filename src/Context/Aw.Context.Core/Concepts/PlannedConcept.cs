using Aw.Context.Core.Engine;
namespace Aw.Context.Core.Concepts;

/// <summary>One concept before it is named: its slug, the feature it grew from and its member features.</summary>
/// <param name="Slug">The id after 'concept:'.</param>
/// <param name="Seed">The key of the seed feature (empty for a pin with no member).</param>
/// <param name="Pin">The owner's pin, or null for a derived concept.</param>
/// <param name="Features">The member feature keys, in key order.</param>
internal sealed record PlannedConcept(string Slug, string Seed, ConceptPin? Pin, IReadOnlyList<string> Features)
{
    /// <summary>The name a concept scan's hint gave the group, or null; blank is as good as null.</summary>
    public string? Name { get; init; }

    /// <summary>The glossary terms of the member features, the most telling first; empty without a glossary.</summary>
    public IReadOnlyList<string> Terms { get; init; } = [];
}
