using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Features.GetLenses;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using TrustLevel = Aw.Context.Core.Results.Trust;

namespace Aw.Context.Tests.GetLenses;

/// <summary>The intent band's records, cited from the record store (#1702): a marker and a permalink for each one the store holds live, nothing for the rest.</summary>
[Trait("Lane", "unit")]
public sealed class LensCitationsTests
{
    private const string LinearUrl = "https://linear.app/split-panes/issue/SPL-103/toggle-split-orientation";
    private const string Key = "linear:issue/7f0c";

    private static readonly HashSet<string> _teams = new(["SPL"], StringComparer.Ordinal);

    private static LensRecord Lens(string node) =>
        new(NodeKey.From(node), "label", "linear", [], TrustLevel.Untrusted, CitationState.Unchecked, [], NodeKind.Record);

    private static IntentBand Band(params string[] nodes) =>
        new([.. nodes.Select(Lens)], [], new IntentCounts(nodes.Length, 0, Orphaned: null));

    private static async Task<(IntentBand Band, IReadOnlyDictionary<string, Citation> Cites)> Cite(IRecordStore store, params string[] nodes) =>
        await LensCitations.Cite(Band(nodes), new LensCitations.Reading(store, _teams, new FakeScrubber(), RecordSamples.Now), CancellationToken.None);

    private static async Task<FakeRecordStore> Holding(params StoredRecord[] records)
    {
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, [.. records.Select(r => RecordSamples.Write(r, "text"))]), CancellationToken.None);
        return store;
    }

    private static StoredRecord Stored(string key, string? url = null) => RecordSamples.Record(key) with { Url = url };

    [Fact]
    public async Task A_stored_linear_record_gets_a_marker_its_permalink_and_its_identifier()
    {
        var (band, cites) = await Cite(await Holding(Stored(Key, LinearUrl)), "record:" + Key);

        var record = Assert.Single(band.Records);
        Assert.Equal(["C1"], record.Cites);
        Assert.Equal(CitationState.Current, record.State);
        var citation = cites["C1"];
        Assert.Equal(new Uri(LinearUrl), citation.Permalink);
        Assert.Equal(["SPL-103"], citation.Aliases);
        Assert.Equal(Key, citation.Key.Value);
        Assert.Equal(TrustLevel.Untrusted, citation.Trust);
        Assert.Equal(CitationState.Current, citation.State);
    }

    [Fact]
    public async Task Two_records_are_marked_in_band_order()
    {
        var (band, cites) = await Cite(await Holding(Stored("linear:issue/aaaa"), Stored("linear:issue/bbbb")), "record:linear:issue/bbbb", "record:linear:issue/aaaa");

        Assert.Equal([["C1"], ["C2"]], band.Records.Select(r => r.Cites));
        Assert.Equal("linear:issue/bbbb", cites["C1"].Key.Value);
        Assert.Equal("linear:issue/aaaa", cites["C2"].Key.Value);
    }

    [Fact]
    public async Task A_record_the_store_does_not_hold_stays_uncited()
    {
        var (band, cites) = await Cite(new FakeRecordStore(), "record:" + Key);

        AssertUncited(band, cites);
    }

    [Fact]
    public async Task A_removed_record_stays_uncited()
    {
        var store = await Holding(Stored(Key, LinearUrl));
        _ = await store.Commit(new RecordBatch(RecordSamples.Linear, [], [RecordKey.From(Key)], new SyncPosition(SourceCursor.From("2"), SyncPhase.Incremental, BackfillCursor: null), RecordSamples.Now), CancellationToken.None);

        var (band, cites) = await Cite(store, "record:" + Key);

        AssertUncited(band, cites);
    }

    [Fact]
    public async Task A_blocked_record_stays_uncited()
    {
        var blocked = RecordSamples.Record(Key, blocked: true);
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, new RecordWrite(blocked, [])), CancellationToken.None);

        var (band, cites) = await Cite(store, "record:" + Key);

        AssertUncited(band, cites);
    }

    [Fact]
    public async Task A_store_that_cannot_be_read_leaves_the_record_uncited_and_the_answer_whole()
    {
        var (band, cites) = await Cite(new FailingRecordStore(), "record:" + Key);

        AssertUncited(band, cites);
    }

    [Fact]
    public async Task A_url_carrying_a_secret_is_cited_with_no_permalink()
    {
        var (band, cites) = await Cite(await Holding(Stored(Key, LinearUrl + "?t=" + FakeScrubber.Secret)), "record:" + Key);

        Assert.Equal(["C1"], Assert.Single(band.Records).Cites);
        Assert.Null(cites["C1"].Permalink);
    }

    [Fact]
    public async Task An_alias_node_stays_uncited()
    {
        var (band, cites) = await Cite(await Holding(Stored(Key, LinearUrl)), "record:SPL-103");

        AssertUncited(band, cites);
    }

    private static void AssertUncited(IntentBand band, IReadOnlyDictionary<string, Citation> cites)
    {
        var record = Assert.Single(band.Records);
        Assert.Empty(record.Cites);
        Assert.Equal(CitationState.Unchecked, record.State);
        Assert.Empty(cites);
    }
}
