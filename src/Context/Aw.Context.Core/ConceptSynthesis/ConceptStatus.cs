namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Where concept synthesis stands, read live from the concept store and the record store (#1597): what 'sindri index status' prints on its concepts line.</summary>
/// <param name="Enabled">Whether the decision pass synthesizes concepts at all (context.conceptSynthesis and labeling both on).</param>
/// <param name="Concepts">How many concepts are stored.</param>
/// <param name="MultiSource">How many of them group records of two or more sources.</param>
/// <param name="Queued">How many wait for the model to name them.</param>
/// <param name="Covered">How many distinct records the named (done) concepts group.</param>
/// <param name="Watermark">The embedding.seq high-water the last pass reached (0: none ran).</param>
/// <param name="Embedded">How many live records (not removed, not blocked) have a chunk vector.</param>
/// <param name="Pending">How many live records have none yet.</param>
public sealed record ConceptStatus(bool Enabled, int Concepts, int MultiSource, int Queued, int Covered, long Watermark, int Embedded, int Pending)
{
    /// <summary>Counts the stored concepts the way a synthesis pass reports them.</summary>
    /// <param name="enabled">Whether synthesis is on.</param>
    /// <param name="concepts">The stored concepts.</param>
    /// <param name="watermark">The saved watermark.</param>
    /// <param name="live">How many live records (not removed, not blocked) the record store holds.</param>
    /// <param name="embedded">How many of them have a chunk vector.</param>
    /// <returns>The status.</returns>
    public static ConceptStatus From(bool enabled, IReadOnlyList<SynthesizedConcept> concepts, long watermark, int live, int embedded) => new(
        enabled,
        concepts.Count,
        concepts.Count(c => c.Members.Select(m => m.Source).Distinct(StringComparer.Ordinal).Skip(1).Any()),
        concepts.Count(c => c.State == SynthesisState.Queued),
        concepts.Where(c => c.State == SynthesisState.Done).SelectMany(c => c.Members.Select(m => m.Record)).Distinct(StringComparer.Ordinal).Count(),
        watermark,
        embedded,
        Math.Max(0, live - embedded));
}
