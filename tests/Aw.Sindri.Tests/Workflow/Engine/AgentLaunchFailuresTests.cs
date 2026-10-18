using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>What a failed launch does to the run (engine P8 D8, launcher L-6).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchFailuresTests
{
    [Fact]
    public void Not_approved_argv_parks_with_SND_LAUNCH_012()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchNotApproved.With("not-approved: aider"), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-012"), (failure.Kind, failure.Code));
        Assert.Contains("SND-LAUNCH-012", failure.Text, StringComparison.Ordinal);
        Assert.Contains("not-approved: aider", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_form_the_entry_lacks_parks_with_SND_LAUNCH_013()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchFormUnsupported.With("policy form is none"), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-013"), (failure.Kind, failure.Code));
    }

    [Fact]
    public void A_host_failure_is_step_failed_launch()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchInterrupted.With("tmux refused"), LaunchStage.Host));

        Assert.Equal(AgentLaunchFailureKind.StepFailed, failure.Kind);
        Assert.StartsWith("launch", failure.Text, StringComparison.Ordinal);
        Assert.Contains("tmux refused", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unavailable_sandbox_is_no_runtime_with_its_reason_as_data()
    {
        var error = SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoBwrap);

        var failure = AgentLaunchFailures.Of(new FailedLaunch(error, LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.NoRuntime, "SND-LAUNCH-014", "no-bwrap", null), (failure.Kind, failure.Code, failure.Reason, failure.Source));
    }

    [Fact]
    public void A_missing_login_is_no_runtime_with_the_source_it_could_not_give()
    {
        var error = SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, source: ModelLoginSource.SeparateLogin);

        var failure = AgentLaunchFailures.Of(new FailedLaunch(error, LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.NoRuntime, "SND-LAUNCH-017", "no-login", "separate-login"), (failure.Kind, failure.Code, failure.Reason, failure.Source));
    }

    [Theory]
    [InlineData("SND-LAUNCH-015", SandboxReasons.FormNone)]
    [InlineData("SND-LAUNCH-016", SandboxReasons.GitRef)]
    public void A_step_that_cannot_be_confined_or_failed_its_self_check_is_a_launch_park(string code, string reason)
    {
        var definition = SandboxCodes.All.Single(c => c.Error.Code == code).Error;

        var failure = AgentLaunchFailures.Of(new FailedLaunch(SandboxRefusal.For(definition, reason), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.LaunchPark, code, reason), (failure.Kind, failure.Code, failure.Reason));
    }

    [Theory]
    [InlineData("SND-LAUNCH-018", SandboxReasons.NoKey, "launch")]
    [InlineData("SND-LAUNCH-021", SandboxReasons.InstallFailed, "launch")]
    [InlineData("SND-LAUNCH-019", SandboxReasons.Memory, "limit")]
    public void A_step_failure_code_carries_the_reason_word_the_catalogue_gives_it(string code, string reason, string word)
    {
        var definition = SandboxCodes.All.Single(c => c.Error.Code == code).Error;

        var failure = AgentLaunchFailures.Of(new FailedLaunch(SandboxRefusal.For(definition, reason), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.StepFailed, word), (failure.Kind, failure.FailureReason));
        Assert.StartsWith(word + ": " + code, failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_full_session_cap_is_a_wait_and_not_a_failure()
    {
        var error = SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions);

        var failure = AgentLaunchFailures.Of(new FailedLaunch(error, LaunchStage.Host));

        Assert.Equal(AgentLaunchFailureKind.Wait, failure.Kind);
    }

    [Fact]
    public void A_failure_that_is_not_a_sandbox_refusal_has_no_reason_or_source()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchInterrupted.With("tmux refused"), LaunchStage.Host));

        Assert.Null(failure.Reason);
        Assert.Null(failure.Source);
        Assert.Null(failure.FailureReason);
    }
}
