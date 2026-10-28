using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.QueryContext;

public sealed class LinearDegradeTests
{
    private static readonly SourceDescriptor _linear = new(SourceId.From("linear"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    private const string IssueKey = "linear:issue/3f2b8c1e-5a47-4d9e-9b6a-0c1d2e3f4a5b";
    private static readonly AudienceKey _audience = AudienceKey.From("linear:SPL");

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-071")]
    [InlineData("SND-CONTEXT-072")]
    public async Task A_linear_freshness_check_that_fails_degrades_the_query_and_names_its_code(string code)
    {
        var error = code == ContextErrors.LinearKeyRefused.Code ? ContextErrors.LinearKeyRefused.With("Linear refused the key") : ContextErrors.LinearUnexpectedReply.With("Linear answered with an unexpected shape");
        var linear = new FakeContextSource(_linear, []) { Failure = error };
        var store = new FakeRecordStore();
        _ = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record(IssueKey) with { Audience = _audience }, "orientation resets on restore")), TestContext.Current.CancellationToken);
        var leg = new FakeLexicalLeg().Add(new ChunkKey(RecordKey.From(IssueKey), 0), _audience);

        var result = await Engines.Over(new FixedSourceCatalog(FakeContextSource.FixtureDocs(), linear), lexical: leg, records: store)
            .Query(new ContextQuery("orientation"), Engines.Human("""{"sources":{"linear":{"teams":["SPL"]}}}"""), TestContext.Current.CancellationToken);

        var degraded = Assert.Single(result.Value.Degraded);
        Assert.Equal(("linear", ContextErrors.SourceUnavailable.Code), (degraded.Source.Value, degraded.Code));
        Assert.StartsWith(code + ":", degraded.Message, StringComparison.Ordinal);
        Assert.Contains(result.Value.Cites.Values, c => c.Key.Value == IssueKey);
    }
}
