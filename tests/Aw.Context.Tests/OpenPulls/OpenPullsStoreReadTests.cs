using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.QueryContext;

namespace Aw.Context.Tests.OpenPulls;

/// <summary>#2154: OpenPulls asks the record store for open and draft PRs only, so the closed and merged history of a repo is never read from it.</summary>
[Trait("Lane", "unit")]
public sealed class OpenPullsStoreReadTests
{
    private static readonly AudienceKey _acme = AudienceKey.From("repo:acme/api");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordWrite Pr(int number, RecordState state) =>
        RecordSamples.Write(RecordSamples.Record(string.Create(CultureInfo.InvariantCulture, $"github:pr/acme/api/{number}"), source: RecordSamples.Github) with { Kind = RecordKind.Pr, State = state, Audience = _acme }, "body");

    [Fact]
    public async Task Open_PRs_are_listed_without_reading_closed_or_merged_rows_from_the_store()
    {
        var inner = new FakeRecordStore();
        var number = 0;
        RecordState[] states = [.. Enumerable.Repeat(RecordState.Open, 3), RecordState.Draft, .. Enumerable.Repeat(RecordState.Closed, 3), .. Enumerable.Repeat(RecordState.Merged, 3)];
        _ = await inner.Commit(RecordSamples.Batch(RecordSamples.Github, 1, [.. states.Select(state => Pr(++number, state))]), Ct);
        var counting = new CountingRecordStore(inner);

        var pulls = await Engines.Over(new FixedSourceCatalog(), records: counting).OpenPulls(new AudienceScope(new HashSet<AudienceKey> { _acme }), Ct);

        Assert.Equal(["github:pr/acme/api/1", "github:pr/acme/api/2", "github:pr/acme/api/3", "github:pr/acme/api/4"], pulls.Value.Select(static r => r.Key.Value), StringComparer.Ordinal);
        Assert.True(counting.ClosedRowsRead == 0, $"#2154: the store returned {counting.ClosedRowsRead.ToString(CultureInfo.InvariantCulture)} closed or merged PR rows, expected none");
    }
}
