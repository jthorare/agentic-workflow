using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.SyncSources;

/// <summary>What one concept synthesis run counts as it goes.</summary>
/// <param name="live">The live records, by key.</param>
/// <param name="stored">The concepts stored when the run began.</param>
/// <param name="watermark">The highest embedding sequence the run read.</param>
internal sealed class ConceptSynthesisPass(Dictionary<string, StoredRecord> live, IReadOnlyList<SynthesizedConcept> stored, long watermark)
{
    public Dictionary<string, StoredRecord> Live { get; } = live;

    public IReadOnlyList<SynthesizedConcept> Stored { get; } = stored;

    public Dictionary<string, SynthesizedConcept> Current { get; } = stored.ToDictionary(c => c.Key, StringComparer.Ordinal);

    public long Watermark { get; } = watermark;

    public required IEgressScrubber Egress { get; init; }

    public int Unchanged { get; set; }

    public int Labeled { get; set; }

    public int Queued { get; set; }

    public int Covered { get; set; }

    public int Tokens { get; set; }

    public bool Stopped { get; set; }

    public string? Code { get; set; }

    public void Fail(string code)
    {
        Code ??= code;
        Stopped = true;
    }

    public ConceptSynthesisReport Report(int groups, int embedded, IReadOnlyList<float> cosines) => new(
        groups,
        Unchanged,
        Labeled,
        Queued,
        Live.Count - embedded,
        Current.Count,
        Current.Values.Count(c => c.Members.Select(m => m.Source).Distinct(StringComparer.Ordinal).Skip(1).Any()),
        Covered,
        embedded,
        [.. CosineHistogram.Buckets(cosines).Select(b => new CosineBucket(b.Lower, b.Count))],
        Tokens,
        Code);
}
