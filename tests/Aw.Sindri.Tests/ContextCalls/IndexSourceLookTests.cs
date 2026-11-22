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
        Assert.Equal(look, IndexSourceLook.For(_sources[name]));

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

        var problem = IndexSourceLook.Problem(backoff);

        Assert.Equal(new HealthProblem("SND-CONTEXT-071", "linear: Linear refused the key (401), retrying", "the linear source needs a look"), problem);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_idle_source_that_found_nothing_names_the_unavailable_code_and_a_fine_one_is_no_problem()
    {
        var empty = IndexSourceLook.Problem(_sources["idle, a pass ended and found nothing"]);

        Assert.Equal(new HealthProblem("SND-CONTEXT-010", "library: a pass ended and found nothing", "the library source needs a look"), empty);
        Assert.Null(IndexSourceLook.Problem(_sources["idle, a pass ended with records"]));
    }

    private static SourceProgress Idle(DateTimeOffset? lastSync, int records, DateTimeOffset? lastCommit) =>
        new(SourceId.From("library"), SyncPhase.Incremental, lastSync, LastCode: null, Usable: true, Complete: true) { Pull = "idle", Records = records, LastCommit = lastCommit };
}
