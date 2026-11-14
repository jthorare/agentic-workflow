using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Shell;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.FreshHome;

/// <summary>A profile file the server can't read (mode 000) is not Healthy: the top bar pill and the System headline say so (issue 3436).</summary>
[Lane(TestLane.Unit)]
public sealed class UnreadableProfileHeadlineTests
{
    private static StatusView Unreadable(string kind = "unreadable") => A.Status() with
    {
        Health = new HealthView("healthy", []),
        Profile = new ProfileView(Live: null, Problem: "/home/thor/profile.yaml can't be read: Access to the path is denied.", Source: null, ProblemKind: kind),
    };

    [Fact]
    public async Task An_invalid_profile_reads_invalid_and_the_pill_is_still_not_Healthy()
    {
        var system = new SystemTestHarness();
        await system.Show(Unreadable("invalid"));

        Assert.Equal("invalid", system.Vm.ProfileState);
        Assert.False(system.Vm.Pill.IsHealthy);
        Assert.Equal(Labels.PillNeedsLook, system.Vm.Pill.Word, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_pill_is_not_Healthy_when_the_profile_is_unreadable()
    {
        var system = new SystemTestHarness();
        await system.Show(Unreadable());

        Assert.Equal("unreadable", system.Vm.ProfileState);
        Assert.False(system.Vm.Pill.IsHealthy);
        Assert.NotEqual("Healthy", system.Vm.Pill.Word, StringComparer.Ordinal);
        Assert.Equal(Labels.PillNeedsLook, system.Vm.Pill.Word, StringComparer.Ordinal);
        Assert.Equal(Labels.HealthWord("healthy"), system.Vm.Pill.ProcessWord, StringComparer.Ordinal);   // the Control plane card keeps the server's word
    }

    [Fact]
    public async Task The_headline_is_amber_and_names_the_profile_when_the_profile_is_unreadable()
    {
        var system = new SystemTestHarness();
        await system.Show(Unreadable());

        Assert.NotEqual(Labels.AllRunning, system.Vm.Sentence, StringComparer.Ordinal);
        Assert.Contains("profile", system.Vm.Sentence, StringComparison.OrdinalIgnoreCase);
        Assert.True(system.Vm.ToneWarn);
        Assert.False(system.Vm.ToneError);
    }
}
