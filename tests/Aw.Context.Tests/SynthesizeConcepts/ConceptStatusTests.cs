using System.Globalization;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.SynthesizeConcepts;

/// <summary>What 'sindri index status' shows of concept synthesis (#1597): counted live from the concept store and the record store.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptStatusTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-29T12:00:00Z", CultureInfo.InvariantCulture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SynthesizedConcept Concept(string key, SynthesisState state, params (string Record, string Source)[] members) =>
        new(key, key, "summary", [.. members.Select(m => new ConceptMember(m.Record, "v1", m.Source))], "hash-" + key, state, Watermark: 7, _at, _at);

    private static List<SynthesizedConcept> Three() =>
    [
        Concept("checkout-retry", SynthesisState.Done, ("github:pr/o/r/1", "github"), ("linear:issue/1", "linear"), ("linear:issue/2", "linear")),
        Concept("pane-resize", SynthesisState.Done, ("linear:issue/2", "linear"), ("linear:issue/3", "linear")),
        Concept("billing", SynthesisState.Queued, ("linear:issue/4", "linear"), ("linear:issue/5", "linear")),
    ];

    [Fact]
    public void From_counts_concepts_multi_source_queued_and_the_distinct_records_of_the_done_ones()
    {
        var status = ConceptStatus.From(enabled: true, Three(), watermark: 8812, live: 562, embedded: 550);

        Assert.Equal(new ConceptStatus(Enabled: true, Concepts: 3, MultiSource: 1, Queued: 1, Covered: 4, Watermark: 8812, Embedded: 550, Pending: 12), status);   // 1, 2 and 3 from the done ones: issue/2 is in both, github pr 1 too
    }

    [Fact]
    public void From_with_no_concepts_counts_nothing_but_still_reports_the_records()
    {
        var status = ConceptStatus.From(enabled: false, [], watermark: 0, live: 10, embedded: 4);

        Assert.Equal(new ConceptStatus(Enabled: false, Concepts: 0, MultiSource: 0, Queued: 0, Covered: 0, Watermark: 0, Embedded: 4, Pending: 6), status);
    }

    [Fact]
    public async Task The_engine_reads_the_stored_concepts_the_watermark_and_the_live_records_and_reports_synthesis_on_when_labeling_is()
    {
        using var world = new MapWorld();
        foreach (var concept in Three())
        {
            _ = await world.ConceptSynthesis.Save(concept, Ct);
        }

        _ = await world.ConceptSynthesis.SaveWatermark(8812, _at, Ct);
        var embedded = RecordSamples.Record("linear:issue/1");
        var waiting = RecordSamples.Record("linear:issue/9");
        _ = await world.Records.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(embedded, "one"), RecordSamples.Write(waiting, "nine")), Ct);
        _ = await world.Records.PutVectors([RecordSamples.Vector(new ChunkKey(embedded.Key, 0), "one", 1, 0)], Ct);

        var status = await world.Engine.ConceptStatus(MapWorld.HumanWith("{\"labeling\":{\"enabled\":true}}"), Ct);

        Assert.True(status.IsSuccess, status.IsError ? status.Error.Message : null);
        Assert.Equal(new ConceptStatus(Enabled: true, Concepts: 3, MultiSource: 1, Queued: 1, Covered: 4, Watermark: 8812, Embedded: 1, Pending: 1), status.Value);
    }

    [Theory]
    [InlineData("{\"labeling\":{\"enabled\":false}}")]
    [InlineData("{\"labeling\":{\"enabled\":true},\"conceptSynthesis\":{\"enabled\":false}}")]
    public async Task The_engine_reports_synthesis_off_when_labeling_or_conceptSynthesis_is_off(string section)
    {
        using var world = new MapWorld();

        var status = await world.Engine.ConceptStatus(MapWorld.HumanWith(section), Ct);

        Assert.True(status.IsSuccess, status.IsError ? status.Error.Message : null);
        Assert.False(status.Value.Enabled);
    }
}
