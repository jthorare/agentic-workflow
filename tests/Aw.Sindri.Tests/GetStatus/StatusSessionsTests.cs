using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ListSessions;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>GET /status gains sessions{live, asking, problems} (collector spec CA-8), additive.</summary>
public sealed class StatusSessionsTests
{
    private readonly ViewsHarness _v = new();
    private readonly InMemorySessionSnapshot _snapshot = new();

    private GetStatusService Service() => new(_v.Slice.Held, new ProfileReads(_v.Slice.Profiles, _v.Slice.Profiles), _v.Slice.Store, _v.Progress, new HealthServiceRows(_v.Views, []), _snapshot);

    // Live, asking, problems, running: the record also carries the problem rows (#4012), which these tests leave to StatusSessionProblemsTests.
    private static (int, int, int, int) Counts(SessionStatus s) => (s.Live, s.Asking, s.Problems, s.Running);

    [Fact, Trait("Lane", "unit")]
    public async Task Status_counts_the_live_asking_and_problem_sessions_of_the_last_pass()
    {
        _snapshot.Publish(new SessionSnapshot(SessionsRig.Three, [], [new SessionProblem("processes", "SND-SESSION-001", "x")], Sessions.SessionData.T0));

        var status = (await Service().Handle(new GetStatusRequest(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((3, 1, 1, 1), Counts(status.Sessions!));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_counts_zero_before_the_first_pass()
    {
        var status = (await Service().Handle(new GetStatusRequest(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal((0, 0, 0, 0), Counts(status.Sessions!));
    }
}
