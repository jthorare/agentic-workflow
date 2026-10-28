using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Kernel;

namespace Aw.Context.Tests.QueryContext;

[Trait("Lane", "unit")]
public sealed class LiveLinearOffTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private const string IssueKey = "linear:issue/3f2b8c1e-5a47-4d9e-9b6a-0c1d2e3f4a5b";
    private static readonly SourceDescriptor _linear = new(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);
    private static readonly AudienceKey _audience = AudienceKey.From("linear:SPL");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Query_with_the_owned_linear_feed_never_searches_linear_live()
    {
        var (store, leg) = await Owned("linear:issue/a");
        var linear = new FakeContextSource(_linear, []);

        _ = await Engines.Over(new FixedSourceCatalog(FakeContextSource.FixtureDocs(), linear), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Empty(linear.Searches);
    }

    [Fact]
    public async Task Query_checks_cited_owned_linear_issues_in_one_batched_linear_call()
    {
        var (store, leg) = await Owned(IssueKey);
        var linear = new VersionCounting(new FakeContextSource(_linear, []));

        var result = await Engines.Over(new FixedSourceCatalog(FakeContextSource.FixtureDocs(), linear), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        Assert.Contains(result.Value.Cites.Values, c => c.Key.Value == IssueKey);   // the owned record is cited, so its freshness is asked
        var batch = Assert.Single(linear.Batches);
        Assert.Contains(batch, key => key == IssueKey);
    }

    private static async Task<(FakeRecordStore Store, FakeLexicalLeg Leg)> Owned(string key)
    {
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(key) with { Audience = _audience }, "orientation resets on restore")), Ct);
        return (store, new FakeLexicalLeg().Add(new ChunkKey(RecordKey.From(key), 0), _audience));
    }

    private sealed class VersionCounting(FakeContextSource inner) : IContextSource
    {
        public List<IReadOnlyList<string>> Batches { get; } = [];

        public SourceDescriptor Describe() => inner.Describe();

        public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => await inner.Search(query, cancellationToken);

        public async ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => await inner.Fetch(reference, cancellationToken);

        public async ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
        {
            Batches.Add([.. refs.Select(r => r.Canonical.Value)]);
            return await inner.Versions(refs, cancellationToken);
        }

        public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => await inner.Probe(cancellationToken);
    }
}
