using Aw.Sindri.Core.Workflow.Engine.Rules;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The desk runtime's timeout (engine spec W20, §3.4, §3.8; rev 8.1 §5.3): the wait is at least 'workflow.deskTimeout'
/// (2d by default); its expiry (parks 'timeout' on the same run, never a retry) is decided by
/// TransitionDecision.Expired and covered in TimerExpiryTests. Lane decision (engine P4 unit (c)): W20 and §3.8
/// win over §5.3's 'step.failed(timeout)'.
/// </summary>
public sealed class DeskTimeoutsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Desk_wait_is_the_state_timeout_raised_to_the_desk_floor()
    {
        var floor = DeskTimeouts.DefaultFloor;

        Assert.Equal(TimeSpan.FromDays(2), floor);
        Assert.Equal(floor, DeskTimeouts.Wait(Step.States["produce"].Timeout, floor));   // 4h
        Assert.Equal(TimeSpan.FromHours(4), DeskTimeouts.Wait(Step.States["produce"].Timeout, TimeSpan.FromMinutes(1)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_decision_context_defaults_to_the_desk_floor() =>
        Assert.Equal(DeskTimeouts.DefaultFloor, EngineFixture.Context().DeskTimeout);   // Core's Decisions namespace can't name DeskTimeouts, so the two defaults are pinned here
}
