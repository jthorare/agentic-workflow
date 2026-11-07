using System.Globalization;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.QueryContext;

namespace Aw.Context.Tests.OpenPulls;

/// <summary>IContextPulls.OpenPulls over a record store (PR review page spec §5.6): the open and draft PRs the caller's audiences allow, paged 500 at a time.</summary>
[Trait("Lane", "unit")]
public sealed class OpenPullsTests
{
    private static readonly AudienceKey _acme = AudienceKey.From("repo:acme/api");
    private static readonly AudienceKey _other = AudienceKey.From("repo:other/web");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AudienceScope Allow(params AudienceKey[] keys) => new(new HashSet<AudienceKey>(keys));

    private static StoredRecord Pr(string repo, int number, RecordState state, AudienceKey audience) =>
        RecordSamples.Record(string.Create(CultureInfo.InvariantCulture, $"github:pr/{repo}/{number}"), source: RecordSamples.Github) with { Kind = RecordKind.Pr, State = state, Audience = audience };

    private static async Task<FakeRecordStore> Store(params StoredRecord[] records)
    {
        var store = new FakeRecordStore();
        foreach (var page in records.Chunk(200))
        {
            _ = await store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, [.. page.Select(static r => RecordSamples.Write(r, "body"))]), Ct);
        }

        return store;
    }

    private static async Task<string[]> Keys(FakeRecordStore store, AudienceScope audience)
    {
        var pulls = await Engines.Over(new FixedSourceCatalog(), records: store).OpenPulls(audience, Ct);
        return [.. pulls.Value.Select(static r => r.Key.Value)];
    }

    [Fact]
    public async Task Open_and_draft_PRs_are_kept_and_closed_and_merged_ones_dropped()
    {
        var store = await Store(Pr("acme/api", 1, RecordState.Open, _acme), Pr("acme/api", 2, RecordState.Draft, _acme), Pr("acme/api", 3, RecordState.Closed, _acme), Pr("acme/api", 4, RecordState.Merged, _acme));

        Assert.Equal(["github:pr/acme/api/1", "github:pr/acme/api/2"], await Keys(store, Allow(_acme)));
    }

    [Fact]
    public async Task A_PR_outside_the_callers_audiences_is_not_returned()
    {
        var store = await Store(Pr("acme/api", 1, RecordState.Open, _acme), Pr("other/web", 2, RecordState.Open, _other));

        Assert.Equal(["github:pr/acme/api/1"], await Keys(store, Allow(_acme)));
        Assert.Empty(await Keys(store, AudienceScope.None));
    }

    [Fact]
    public async Task Open_PRs_past_the_first_500_are_still_returned()
    {
        // 520 closed PRs sort first, so the one open PR sits on the second page of 500
        var rows = Enumerable.Range(1, 520).Select(n => Pr("acme/api", n, RecordState.Closed, _acme)).Append(Pr("acme/zed", 1, RecordState.Open, _acme)).ToArray();
        var store = await Store(rows);

        Assert.Equal(["github:pr/acme/zed/1"], await Keys(store, Allow(_acme)));
    }

    [Fact]
    public async Task Exactly_500_records_end_the_read_after_one_more_empty_page()
    {
        var rows = Enumerable.Range(1, 500).Select(n => Pr("acme/api", n, RecordState.Open, _acme)).ToArray();
        var store = await Store(rows);

        Assert.Equal(500, (await Keys(store, Allow(_acme))).Length);
    }
}
