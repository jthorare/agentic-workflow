using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class HealthHeadlineTests
{
    private static StatusView With(HealthView? health, ProfileView? profile = null) =>
        A.Status(profile: profile) with { Health = health };

    [Fact]
    public async Task A_healthy_running_control_plane_has_a_plain_headline_and_no_problem_lines()
    {
        var system = new SystemTestHarness();

        await system.Show(With(new HealthView("healthy", [])));

        Assert.Equal(("Everything is running.", false, false, false), (system.Vm.Sentence, system.Vm.ToneWarn, system.Vm.ToneError, system.Vm.HasProblemLines));
    }

    [Fact]
    public async Task A_degraded_headline_is_amber_with_one_line_per_problem_and_its_code()
    {
        var system = new SystemTestHarness();

        await system.Show(With(new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "service heartbeat failed")])));

        var problem = Assert.Single(system.Vm.ProblemLines);
        Assert.Equal(("SND-SCHED-001", "service heartbeat failed", true, false, true), (problem.Code, problem.Text, system.Vm.ToneWarn, system.Vm.ToneError, system.Vm.HasProblemLines));
        Assert.Contains("service heartbeat failed", system.Vm.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stalled_headline_is_red()
    {
        var system = new SystemTestHarness();

        await system.Show(With(new HealthView("stalled", [new HealthProblemView("SND-VIEW-001", "views are stalled")])));

        Assert.Equal((false, true), (system.Vm.ToneWarn, system.Vm.ToneError));
    }

    [Fact]
    public async Task A_rebuilding_headline_is_neutral()
    {
        var system = new SystemTestHarness();

        await system.Show(With(new HealthView("rebuilding", [])));

        Assert.Equal((false, false), (system.Vm.ToneWarn, system.Vm.ToneError));
    }

    [Fact]
    public async Task A_profile_that_needs_approval_is_amber_with_the_command_to_run()
    {
        var system = new SystemTestHarness();

        await system.Show(With(new HealthView("healthy", []), A.ChangedProfile()));

        Assert.Equal((true, false, true), (system.Vm.ToneWarn, system.Vm.ToneError, system.Vm.HasProfileFix));
        Assert.Equal(Labels.ProfileNeedsApproval, system.Vm.Sentence);
    }

    [Fact]
    public void Not_running_is_red()
    {
        var system = new SystemTestHarness();

        _ = system.Vm.NotRunning("nothing answered");

        Assert.Equal((false, true), (system.Vm.ToneWarn, system.Vm.ToneError));
    }
}
