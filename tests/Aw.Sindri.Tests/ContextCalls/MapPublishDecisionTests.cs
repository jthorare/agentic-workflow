using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>When a refreshed map becomes the current one (code index spec 6.4): a table of cases over a fake clock.</summary>
public sealed class MapPublishDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _wait = TimeSpan.FromMinutes(15);

    private static IndexOutlookDetail Of(IndexOutlook o, RepoWait w = RepoWait.None) => new(o, w, RetryAt: null);

    private static MapHoldState Hold(DateTimeOffset since, params string[] repos) =>
        new(14, since, repos.ToDictionary(static r => r, static r => new MapRepoHead("abc1234", OnDefaultBranch: true) { IndexCommit = "abc1234" }, StringComparer.Ordinal));

    private static MapPublishAction Decide(MapHoldState hold, Dictionary<string, IndexOutlookDetail> outlooks, int? draft = 14, int? current = 13, TimeSpan? wait = null, DateTimeOffset? now = null) =>
        MapPublishDecision.Decide(new MapPublishInput(hold, draft, current, outlooks, wait ?? _wait), now ?? _now);

    private static Dictionary<string, IndexOutlookDetail> Outlooks(params (string Repo, IndexOutlook Outlook)[] o) => o.ToDictionary(static x => x.Repo, static x => Of(x.Outlook), StringComparer.Ordinal);

    private static void IsPublish(MapPublishAction action, MapPublishReason reason, params string[] stale)
    {
        var publish = Assert.IsType<MapPublish>(action);
        Assert.Equal(reason, publish.Reason);
        Assert.Equal(stale, publish.StaleRepos);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_repo_built_publishes_IndexReady() =>
        IsPublish(Decide(Hold(_now, "a", "b"), Outlooks(("a", IndexOutlook.Built), ("b", IndexOutlook.Built))), MapPublishReason.IndexReady);

    [Fact, Trait("Lane", "unit")]
    public void A_held_draft_built_before_its_code_index_is_rebuilt_not_published()
    {
        var hold = new MapHoldState(14, _now, new Dictionary<string, MapRepoHead>(StringComparer.Ordinal)
        {
            ["a"] = new MapRepoHead("abc1234", OnDefaultBranch: true),   // built with no code index
            ["b"] = new MapRepoHead("abc1234", OnDefaultBranch: true) { IndexCommit = "def5678" },   // built against another commit's index
            ["c"] = new MapRepoHead("abc1234", OnDefaultBranch: true) { IndexCommit = "abc1234" },
        });

        var action = Decide(hold, Outlooks(("a", IndexOutlook.Built), ("b", IndexOutlook.Built), ("c", IndexOutlook.Built)));

        Assert.Equal(["a", "b"], Assert.IsType<MapRebuild>(action).Repos);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_built_before_a_code_index_that_is_still_coming_keeps_waiting()
    {
        var hold = new MapHoldState(14, _now, new Dictionary<string, MapRepoHead>(StringComparer.Ordinal) { ["a"] = new MapRepoHead("abc1234", OnDefaultBranch: true) });

        _ = Assert.IsType<MapWait>(Decide(hold, Outlooks(("a", IndexOutlook.Coming))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_built_with_its_code_index_still_publishes_IndexReady() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Built))), MapPublishReason.IndexReady);

    [Fact, Trait("Lane", "unit")]
    public void One_coming_repo_inside_the_wait_waits_until_since_plus_wait_on_one()
    {
        var since = _now.AddMinutes(-5);
        Assert.Equal(new MapWait(since + _wait, 1), Decide(Hold(since, "a", "b"), Outlooks(("a", IndexOutlook.Built), ("b", IndexOutlook.Coming))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_past_the_bound_publishes_TimedOut_naming_the_repo() =>
        IsPublish(Decide(Hold(_now.AddMinutes(-16), "a", "b"), Outlooks(("a", IndexOutlook.Built), ("b", IndexOutlook.Coming))), MapPublishReason.TimedOut, "b");

    [Fact, Trait("Lane", "unit")]
    public void The_clock_exactly_at_the_bound_publishes() =>
        IsPublish(Decide(Hold(_now - _wait, "a"), Outlooks(("a", IndexOutlook.Coming))), MapPublishReason.TimedOut, "a");

    [Fact, Trait("Lane", "unit")]
    public void A_clock_one_tick_before_the_bound_still_waits() =>
        Assert.IsType<MapWait>(Decide(Hold(_now - _wait + TimeSpan.FromTicks(1), "a"), Outlooks(("a", IndexOutlook.Coming))));

    [Fact, Trait("Lane", "unit")]
    public void Backoff_waits_and_then_times_out()
    {
        var o = Outlooks(("a", IndexOutlook.Backoff));
        Assert.Equal(new MapWait(_now.AddMinutes(-1) + _wait, 1), Decide(Hold(_now.AddMinutes(-1), "a"), o));
        IsPublish(Decide(Hold(_now.AddMinutes(-20), "a"), o), MapPublishReason.TimedOut, "a");
    }

    [Fact, Trait("Lane", "unit")]
    public void Broken_publishes_IndexBroken_at_once_even_inside_the_wait() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Broken))), MapPublishReason.IndexBroken, "a");

    [Fact, Trait("Lane", "unit")]
    public void Broken_with_another_repo_coming_names_both_as_stale() =>
        IsPublish(Decide(Hold(_now, "b", "a"), Outlooks(("a", IndexOutlook.Broken), ("b", IndexOutlook.Coming))), MapPublishReason.IndexBroken, "a", "b");

    [Fact, Trait("Lane", "unit")]
    public void Absent_repos_are_ignored_and_all_absent_is_NoIndex()
    {
        IsPublish(Decide(Hold(_now, "a", "b"), Outlooks(("a", IndexOutlook.Absent), ("b", IndexOutlook.Absent))), MapPublishReason.NoIndex);
        IsPublish(Decide(Hold(_now, "a", "b"), Outlooks(("a", IndexOutlook.Absent), ("b", IndexOutlook.Built))), MapPublishReason.IndexReady);
        Assert.Equal(new MapWait(_now + _wait, 1), Decide(Hold(_now, "a", "b"), Outlooks(("a", IndexOutlook.Absent), ("b", IndexOutlook.Coming))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_outlook_entry_reads_as_absent() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks()), MapPublishReason.NoIndex);

    [Fact, Trait("Lane", "unit")]
    public void A_repo_off_the_default_branch_is_absent_by_its_outlook()
    {
        var detail = IndexOutlookRules.Classify(new IndexOutlookInput(IndexCommit: null, HoldHead: "abc1234", HoldOnDefaultBranch: false, LiveHead: "abc1234", InScope: true, ServiceActive: true, BuildRunning: false, NeverBuilt: false, FinishedNewestFirst: []), _now);
        var hold = new MapHoldState(14, _now, new Dictionary<string, MapRepoHead>(StringComparer.Ordinal) { ["a"] = new("abc1234", OnDefaultBranch: false) });
        IsPublish(Decide(hold, new Dictionary<string, IndexOutlookDetail>(StringComparer.Ordinal) { ["a"] = detail }), MapPublishReason.NoIndex);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_current_version_is_FirstMap_naming_the_repos_it_would_have_waited_on() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Coming)), current: null), MapPublishReason.FirstMap, "a");

    [Fact, Trait("Lane", "unit")]
    public void A_wait_of_zero_is_WaitOff() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Coming)), wait: TimeSpan.Zero), MapPublishReason.WaitOff, "a");

    [Fact, Trait("Lane", "unit")]
    public void Built_outranks_WaitOff_and_FirstMap() =>
        IsPublish(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Built)), current: null, wait: TimeSpan.Zero), MapPublishReason.IndexReady);

    [Fact, Trait("Lane", "unit")]
    public void Broken_outranks_FirstMap_and_WaitOff() =>
        Assert.Equal(MapPublishReason.IndexBroken, Assert.IsType<MapPublish>(Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Broken)), current: null, wait: TimeSpan.Zero)).Reason);

    [Fact, Trait("Lane", "unit")]
    public void A_draft_that_is_not_the_holds_is_dropped_as_Gone()
    {
        Assert.Equal(new MapDrop(MapDropReason.Gone), Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Built)), draft: 15));
        Assert.Equal(new MapDrop(MapDropReason.Gone), Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Built)), draft: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_current_version_equal_to_the_holds_is_dropped_as_AlreadyCurrent_before_Gone() =>
        Assert.Equal(new MapDrop(MapDropReason.AlreadyCurrent), Decide(Hold(_now, "a"), Outlooks(("a", IndexOutlook.Built)), draft: null, current: 14));

    [Fact, Trait("Lane", "unit")]
    public void Two_repos_one_built_and_one_coming_past_the_bound_have_one_stale() =>
        IsPublish(Decide(Hold(_now.AddMinutes(-30), "a", "b"), Outlooks(("a", IndexOutlook.Built), ("b", IndexOutlook.Coming))), MapPublishReason.TimedOut, "b");

    [Fact, Trait("Lane", "unit")]
    public void A_since_in_the_future_is_clamped_to_now_so_a_clock_stepped_back_cannot_lengthen_the_wait() =>
        Assert.Equal(new MapWait(_now + _wait, 1), Decide(Hold(_now.AddHours(3), "a"), Outlooks(("a", IndexOutlook.Coming))));
}
