using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class StoreBriefSourceTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

    private static BriefScope Scope(TempHome home, params string[] teams) =>
        new(new ContextSettings { LinearTeams = teams }, [new ContextRepo(ContextRepoName.From("aw-split-panes"), "/nonexistent")], home.Scrubbers);

    private static async Task<FakeRecordStore> Stored(params RecordWrite[] writes)
    {
        var store = new FakeRecordStore();
        var committed = await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, writes), TestContext.Current.CancellationToken);
        Assert.True(committed.IsSuccess);
        return store;
    }

    private static RecordWrite Issue(int n, string team = "SPL", int created = 0, int updated = 1, params string[] chunks) =>
        LinearIssueSeed.Issue(
            string.Create(CultureInfo.InvariantCulture, $"0e5a1b00-0000-4000-8000-{n:D12}"), string.Create(CultureInfo.InvariantCulture, $"{team}-{n}"), string.Create(CultureInfo.InvariantCulture, $"Title {n}"),
            _t0.AddDays(created), _t0.AddDays(updated), chunks.Length == 0 ? [string.Create(CultureInfo.InvariantCulture, $"text of {n}")] : chunks);

    [Fact, Trait("Lane", "unit")]
    public async Task Issues_lists_the_profile_teams_in_key_order_with_their_dates_and_never_reports_a_cap()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored(Issue(102), Issue(101), Issue(7, team: "OTH")));

        var listing = (await source.Issues(Scope(home, "SPL"), since: null, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["SPL-101", "SPL-102"], listing.Issues.Select(i => i.Key), StringComparer.Ordinal);
        Assert.Empty(listing.CappedTeams);
        Assert.Equal(_t0, listing.Issues[0].CreatedAt);
        Assert.Equal(_t0.AddDays(1), listing.Issues[0].UpdatedAt);
        Assert.Equal("linear:issue/0e5a1b00-0000-4000-8000-000000000101", listing.Issues[0].RecordRef);
        Assert.Equal("[SPL-101] Title 101", listing.Issues[0].Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Issues_with_a_window_start_drops_what_was_updated_before_it()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored(Issue(101, updated: 1), Issue(102, updated: 5), Issue(103, updated: 9)));

        var listing = (await source.Issues(Scope(home, "SPL"), _t0.AddDays(5), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["SPL-102", "SPL-103"], listing.Issues.Select(i => i.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Issues_skips_records_without_a_creation_time_and_other_kinds_of_title()
    {
        using var home = new TempHome();
        var undated = LinearIssueSeed.Issue("0e5a1b00-0000-4000-8000-000000000201", "SPL-201", "No dates", _t0, _t0, "x");
        undated = undated with { Record = undated.Record with { CreatedAt = null } };
        var source = new StoreBriefSource(await Stored(Issue(101), undated));

        var listing = (await source.Issues(Scope(home, "SPL"), since: null, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["SPL-101"], listing.Issues.Select(i => i.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Issues_pages_through_the_store_so_a_large_team_is_not_cut()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored([.. Enumerable.Range(1, 1_250).Select(n => Issue(n))]));

        var listing = (await source.Issues(Scope(home, "SPL"), since: null, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(1_250, listing.Issues.Count);
        Assert.Empty(listing.CappedTeams);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Issues_without_teams_is_081()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored(Issue(101)));

        var issues = await source.Issues(Scope(home), since: null, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.NoBriefSource.Code, issues.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Brief_is_the_title_and_the_stored_chunks()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored(Issue(101, chunks: ["first part", "second part"])));
        var issue = (await source.Issues(Scope(home, "SPL"), since: null, TestContext.Current.CancellationToken)).Value.Issues[0];

        var brief = (await source.Brief(Scope(home, "SPL"), issue, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("SPL-101", brief!.Key);
        Assert.Contains("first part", brief.Text, StringComparison.Ordinal);
        Assert.Contains("second part", brief.Text, StringComparison.Ordinal);
        Assert.Contains("Title 101", brief.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Brief_of_an_issue_the_store_does_not_hold_is_null_not_an_error()
    {
        using var home = new TempHome();
        var source = new StoreBriefSource(await Stored(Issue(101)));
        var gone = new BriefIssue("SPL-999", "linear:issue/0e5a1b00-0000-4000-8000-000000000999", "v", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        var brief = await source.Brief(Scope(home, "SPL"), gone, TestContext.Current.CancellationToken);

        Assert.True(brief.IsSuccess);
        Assert.Null(brief.Value);
    }
}
