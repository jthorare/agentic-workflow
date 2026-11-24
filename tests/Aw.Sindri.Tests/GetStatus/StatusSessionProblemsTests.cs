using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// The System page's Agents card names its problems (#4012): GET /status lists each counted session problem with its code and sentence, a failed source or a
/// collector that can't run makes the health degraded through the same rows a flagged card uses (#4010), a pass that overran its budget is listed but leaves
/// the health healthy (slow is not broken), and a test override is neither counted nor listed.
/// </summary>
public sealed class StatusSessionProblemsTests
{
    private static readonly SessionProblem _override = new("collector", "SND-SESSION-003", "A test override is set in this run: the sessions shown may be fixtures");
    private static readonly SessionProblem _failed = new("claude-records", "SND-SESSION-001", "The claude-records source couldn't be read in the last pass");

    private readonly ViewsHarness _v = new();
    private readonly InMemorySessionSnapshot _snapshot = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GetStatusResponse> Status(params SessionProblem[] problems)
    {
        _v.Slice.Profiles.Live = SliceHarness.Profile("5s");
        _ = await _v.Slice.HoldLease();
        _v.Publish(stalledAt: null);
        _snapshot.Publish(new SessionSnapshot(SessionsRig.Three, [], problems, Sessions.SessionData.T0));
        var service = new GetStatusService(_v.Slice.Held, new ProfileReads(_v.Slice.Profiles, _v.Slice.Profiles), _v.Slice.Store, _v.Progress, new HealthServiceRows(_v.Views, []), _snapshot);
        return (await service.Handle(new GetStatusRequest(), Token)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_override_is_not_counted_listed_or_a_health_problem()
    {
        var status = await Status(_override);

        Assert.Equal(0, status.Sessions!.Problems);
        Assert.Empty(status.Sessions.ProblemRows);
        Assert.Equal(HealthWord.Healthy, status.Health!.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_source_is_counted_listed_with_its_code_and_makes_health_degraded()
    {
        var status = await Status(_override, _failed);

        Assert.Equal(1, status.Sessions!.Problems);
        Assert.Equal([_failed], status.Sessions.ProblemRows);
        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        var problem = Assert.Single(status.Health.Problems);
        Assert.Equal(("SND-SESSION-001", _failed.Text), (problem.Code, problem.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_collector_that_cannot_read_the_process_table_makes_health_degraded()
    {
        var unreadable = new SessionProblem("processes", "SND-SESSION-002", "Sindri can't see agent sessions on this system: /proc can't be read") { Reason = UnavailableReason.ProcUnreadable };

        var status = await Status(unreadable);

        Assert.Equal(1, status.Sessions!.Problems);
        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        Assert.Equal(["SND-SESSION-002"], status.Health.Problems.Select(p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_collector_the_owner_turned_off_is_listed_but_not_a_health_problem()
    {
        var off = new SessionProblem("collector", "SND-SESSION-002", "Sindri can't see agent sessions on this system: launcher.json turned it off") { Reason = UnavailableReason.Disabled };

        var status = await Status(off);

        Assert.Equal([off], status.Sessions!.ProblemRows);
        Assert.Equal(HealthWord.Healthy, status.Health!.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_that_overran_its_budget_is_listed_on_the_card_and_leaves_the_health_healthy()
    {
        var status = await Status(SessionProblems.Overrun);

        Assert.Equal(1, status.Sessions!.Problems);
        Assert.Equal([SessionProblems.Overrun], status.Sessions.ProblemRows);
        Assert.Equal(HealthWord.Healthy, status.Health!.Word);
        Assert.DoesNotContain(status.Health.Problems, p => string.Equals(p.Code, "SND-SESSION-001", StringComparison.Ordinal));
    }
}
