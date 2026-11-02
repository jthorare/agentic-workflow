using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The tray's view for each shell state (§5's table, U0's three icons): it says "running" only once connected.</summary>
[Lane(TestLane.Unit)]
public sealed class TrayViewsTests
{
    [Theory]
    [InlineData(ShellState.Normal, TrayState.Neutral, Labels.TrayRunning)]
    [InlineData(ShellState.NotRunning, TrayState.NotRunning, Labels.TrayNotRunning)]
    [InlineData(ShellState.Stale, TrayState.Stale, Labels.TrayNotResponding)]
    [InlineData(ShellState.Unreadable, TrayState.Stale, Labels.TrayNotResponding)]
    [InlineData(ShellState.Connecting, TrayState.Stale, Labels.TrayNotResponding)]
    [InlineData(ShellState.VersionMismatch, TrayState.Stale, Labels.TrayNotResponding)]
    internal void Each_state_maps_to_its_icon_and_tooltip(ShellState state, TrayState icon, string tooltip) =>
        Assert.Equal(new TrayView(icon, tooltip, Labels.TrayOpen, Labels.TrayQuit), AttentionSummary.For(feed: null, state, default).Tray);
}
