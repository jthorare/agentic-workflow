using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Scheduling;

/// <summary>The one rule for what a human may do to a service (the list's `actions`, and the run, pause and resume refusals).</summary>
public sealed class ServiceActionsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(true, false, false, false, "run,pause")]
    [InlineData(true, false, true, false, "run,resume")]
    [InlineData(true, false, false, true, "pause")]
    [InlineData(true, false, true, true, "resume")]
    [InlineData(false, true, false, false, "run")]
    [InlineData(false, true, false, true, "")]
    [InlineData(true, true, false, false, "run,pause")]
    public void Allowed_follows_the_refusals_and_the_state(bool named, bool builtIn, bool paused, bool jobRunning, string expected) =>
        Assert.Equal(expected, string.Join(',', ServiceActions.Allowed(named, builtIn, paused, jobRunning)));

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void A_run_is_admitted_for_a_profile_service_or_a_built_in(bool named, bool builtIn, bool expected) =>
        Assert.Equal(expected, ServiceActions.CanRun(named, builtIn));

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Pause_and_resume_apply_to_profile_services_only(bool named, bool expected) =>
        Assert.Equal(expected, ServiceActions.CanPauseOrResume(named));
}
