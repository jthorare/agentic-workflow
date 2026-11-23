using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An idle source that holds nothing needs a look only once a pass has ended and still found nothing (issue #3430, issue #3183): a fresh home's sources are not a look before their first pass.</summary>
public sealed class IndexSourceLookTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, SourceProgress> _sources = new(StringComparer.Ordinal)
    {
        ["idle, no pass ended yet"] = Idle(lastSync: null, records: 0, lastCommit: null),
        ["idle, a pass ended and found nothing"] = Idle(_now.AddMinutes(-5), records: 0, lastCommit: null),
        ["idle, a pass ended and committed an empty source"] = Idle(_now.AddMinutes(-5), records: 0, _now.AddMinutes(-5)),
        ["idle, a pass ended with records"] = Idle(_now.AddMinutes(-5), records: 40, _now.AddMinutes(-5)),
        ["idle, no pass ended yet, records from an earlier run"] = Idle(lastSync: null, records: 40, _now.AddDays(-1)),
    };

    public static TheoryData<string, bool> Cases => new()
    {
        { "idle, no pass ended yet", false },
        { "idle, a pass ended and found nothing", true },
        { "idle, a pass ended and committed an empty source", false },
        { "idle, a pass ended with records", false },
        { "idle, no pass ended yet, records from an earlier run", false },
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void An_idle_source_with_nothing_needs_a_look_only_after_a_pass_ended(string name, bool look) =>
        Assert.Equal(look, IndexSourceLook.For(_sources[name], TimeSpan.Zero, _now).NeedsLook);

    [Fact, Trait("Lane", "unit")]
    public void A_source_with_no_finished_pass_needs_no_look_at_14_min_59_s_3909() =>
        Assert.Equal(new SourceLook(NeedsLook: false, Words: null), IndexSourceLook.For(Idle(lastSync: null, records: 0, lastCommit: null), new TimeSpan(0, 14, 59), _now));

    [Fact, Trait("Lane", "unit")]
    public void A_source_with_no_finished_pass_needs_a_look_at_15_min_with_words_3909() =>
        Assert.Equal(new SourceLook(NeedsLook: true, "library hasn't finished a sync in 15 minutes"), IndexSourceLook.For(Idle(lastSync: null, records: 0, lastCommit: null), TimeSpan.FromMinutes(15), _now));

    [Fact, Trait("Lane", "unit")]
    public void A_running_first_read_with_no_finished_pass_needs_a_look_at_15_min_3909() =>
        Assert.True(IndexSourceLook.For(Idle(lastSync: null, records: 0, lastCommit: null) with { Pull = "running" }, TimeSpan.FromMinutes(15), _now).NeedsLook);

    [Fact, Trait("Lane", "unit")]
    public void A_source_with_a_finished_pass_ignores_the_timer_3832() =>
        Assert.Equal(new SourceLook(NeedsLook: false, Words: null), IndexSourceLook.For(Idle(_now.AddMinutes(-5), records: 40, _now.AddMinutes(-5)), TimeSpan.FromHours(5), _now));

    [Fact, Trait("Lane", "unit")]
    public void A_source_in_backoff_is_one_problem_with_its_code_and_error()
    {
        var backoff = new SourceProgress(SourceId.From("linear"), Phase: null, _now.AddMinutes(-4), LastCode: null, Usable: false, Complete: false)
        {
            Pull = "backoff",
            Records = 0,
            LastError = "SND-CONTEXT-071",
            LastErrorText = "Linear refused the key (401)",
            Failures = 3,
            NextTry = _now.AddMinutes(30),
        };

        var problem = IndexSourceLook.Problem(backoff, TimeSpan.Zero, _now);

        Assert.Equal(new HealthProblem("SND-CONTEXT-071", "linear: Linear refused the key (401), retrying", "the linear source needs a look"), problem);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_idle_source_that_found_nothing_names_the_unavailable_code_and_a_fine_one_is_no_problem()
    {
        var empty = IndexSourceLook.Problem(_sources["idle, a pass ended and found nothing"], TimeSpan.Zero, _now);

        Assert.Equal(new HealthProblem("SND-CONTEXT-010", "library: a pass ended and found nothing", "the library source needs a look"), empty);
        Assert.Null(IndexSourceLook.Problem(_sources["idle, a pass ended with records"], TimeSpan.Zero, _now));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_source_past_the_first_pass_limit_is_a_problem_with_the_limit_words_and_none_while_the_run_time_is_zero()
    {
        var fresh = Idle(lastSync: null, records: 0, lastCommit: null);

        var late = IndexSourceLook.Problem(fresh, TimeSpan.FromMinutes(15), _now);

        Assert.Equal(new HealthProblem("SND-CONTEXT-010", "library hasn't finished a sync in 15 minutes", "the library source needs a look"), late);
        Assert.Null(IndexSourceLook.Problem(fresh, TimeSpan.Zero, _now));   // a paused index-sync passes zero: the health agrees with the index row
    }

    public static TheoryData<bool, int, bool> FirstPassCommits => new()
    {
        { false, -1, false },   // no tick ended yet, a commit 1 min ago
        { true, -14, false },   // a tick ended, a commit 14 min ago
        { true, -15, true },    // a tick ended, no commit for 15 min
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(FirstPassCommits))]
    public void A_first_pass_needs_a_look_only_when_no_record_was_committed_within_15_minutes_4035(bool tickEnded, int commitMinutes, bool look)
    {
        var lastSync = tickEnded ? _now.AddMinutes(commitMinutes) : (DateTimeOffset?)null;
        var source = FirstPass(lastSync, _now.AddMinutes(commitMinutes));

        Assert.Equal(look, IndexSourceLook.For(source, TimeSpan.FromHours(2), _now).NeedsLook);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_first_pass_with_no_commit_for_15_minutes_is_a_problem_with_its_words_4035() =>
        Assert.Equal(
            new HealthProblem("SND-CONTEXT-010", "linear hasn't committed a record in 15 minutes", "the linear source needs a look"),
            IndexSourceLook.Problem(FirstPass(_now.AddMinutes(-20), _now.AddMinutes(-20)), TimeSpan.FromMinutes(20), _now));

    private static SourceProgress FirstPass(DateTimeOffset? lastSync, DateTimeOffset lastCommit) =>
        new(SourceId.From("linear"), SyncPhase.Backfill, lastSync, LastCode: null, Usable: true, Complete: false) { Pull = "running", Records = 12_400, LastCommit = lastCommit };

    private static SourceProgress Idle(DateTimeOffset? lastSync, int records, DateTimeOffset? lastCommit) =>
        new(SourceId.From("library"), SyncPhase.Incremental, lastSync, LastCode: null, Usable: true, Complete: true) { Pull = "idle", Records = records, LastCommit = lastCommit };
}
