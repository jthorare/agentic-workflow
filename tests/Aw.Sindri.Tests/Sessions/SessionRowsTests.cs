using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Tests.ListSessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The shared lookup (collector spec CA-2, CA-9).</summary>
public sealed class SessionRowsTests
{
    private static readonly SessionSnapshot _pass = new(SessionsRig.Three, [], [], SessionData.T0);

    [Fact, Trait("Lane", "unit")]
    public void Find_answers_the_session_for_its_ref_and_null_for_an_unknown_one()
    {
        Assert.NotNull(SessionRows.Find(_pass, SessionsRig.Claude().Ref));
        Assert.Null(SessionRows.Find(new SessionSnapshot([], [], [], SessionData.T0), SessionsRig.Claude().Ref));
    }

    [Fact, Trait("Lane", "unit")]
    public void Unavailable_reads_the_typed_reason_not_the_sentence()
    {
        var reworded = new SessionProblem("processes", "SND-SESSION-002", "any wording") { Reason = UnavailableReason.ProcUnreadable };
        var sentenceOnly = new SessionProblem("processes", "SND-SESSION-002", "Sindri can't see agent sessions on this system: disabled");

        Assert.Equal("proc-unreadable", SessionRows.Unavailable([reworded])!.Message);
        Assert.Null(SessionRows.Unavailable([sentenceOnly]));
    }
}
