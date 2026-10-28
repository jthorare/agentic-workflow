using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Kernel;

namespace Aw.Context.Tests.QueryContext;

[Trait("Lane", "unit")]
public sealed class OwnedLinearFreshnessTests
{
    private const string Teams = """{"sources":{"linear":{"teams":["SPL"]}}}""";
    private const string Uuid = "3f2b8c1e-5a47-4d9e-9b6a-0c1d2e3f4a5b";
    private const string IssueKey = "linear:issue/" + Uuid;
    private const string Version = "2026-10-01T09:30:00.0000000Z";
    private static readonly SourceDescriptor _linear = new(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);
    private static readonly AudienceKey _audience = AudienceKey.From("linear:SPL");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unchanged_owned_linear_issue_stays_current_when_linear_is_asked_by_its_bare_id()
    {
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(IssueKey, Version) with { Audience = _audience }, "orientation resets on restore")), Ct);
        var leg = new FakeLexicalLeg().Add(new ChunkKey(RecordKey.From(IssueKey), 0), _audience);

        var result = await Engines.Over(new FixedSourceCatalog(FakeContextSource.FixtureDocs(), new ById()), lexical: leg, records: store).Query(new ContextQuery("orientation"), Engines.Human(Teams), Ct);

        var cite = Assert.Single(result.Value.Cites.Values, c => c.Key.Value == IssueKey);
        Assert.Equal(CitationState.Current, cite.State);
    }

    /// <summary>Answers the way LinearRecords does: by the bare UUID of the issue, and null (gone) for anything else.</summary>
    private sealed class ById : IContextSource
    {
        public SourceDescriptor Describe() => _linear;

        public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<SourceHit>>>(Array.Empty<SourceHit>());

        public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<IReadOnlyList<SourceVersion?>>>(refs.Select(r => r.Id.Value == Uuid ? (SourceVersion?)SourceVersion.From(Version) : null).ToList());

        public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => ValueTask.FromResult(new SourceHealth(SourceState.Ok, "fake", Code: null));
    }
}
