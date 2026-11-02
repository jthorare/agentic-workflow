using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Setup;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Setup;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>
/// #1529: the profile approval's panel is drawn only on the profile step. On every other step the step has no profile, the panel's binding fails, and a failed
/// binding leaves IsVisible at its default (true): an empty Approve button and blank space under every open step.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SetupProfilePanelTests : FrameHost
{
    private async Task<ShellWindow> OnSettings(string scenario)
    {
        Server.SetupAnswer = SetupFixture.Answer(scenario);
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0, 1440, 900);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) };
        shell.GoTo(PageId.Settings);
        Settle(window);
        return window;
    }

    private static SetupStepRow OpenRow(Window window) =>
        window.GetVisualDescendants().OfType<SetupStepRow>().Single(r => r.IsEffectivelyVisible && r.GetVisualDescendants().OfType<Expander>().First(e => string.Equals(e.Name, "SetupStepExpander", StringComparison.Ordinal)).IsExpanded);

    [Fact]
    public Task An_open_step_that_is_not_the_profile_step_draws_no_profile_panel() => OnUi(async () =>
    {
        var window = await OnSettings("first-run");
        var row = OpenRow(window);

        Assert.Equal(("repo", true), (((SetupStepViewModel)row.DataContext!).Id, Named<StackPanel>(row, "SetupStepBody").IsEffectivelyVisible));   // the open step's body is on screen
        Assert.False(Named<SetupProfileConfirmView>(row, "SetupStepProfileConfirm").IsEffectivelyVisible);
    });

    [Fact]
    public Task The_open_profile_step_with_an_approval_draws_its_panel_and_the_Approve_button() => OnUi(async () =>
    {
        var window = await OnSettings("profile-needs-approval");
        var row = OpenRow(window);

        Assert.Equal("profile", ((SetupStepViewModel)row.DataContext!).Id);
        Assert.True(Named<SetupProfileConfirmView>(row, "SetupStepProfileConfirm").IsEffectivelyVisible);
        Assert.True(Named<Button>(row, "SetupProfileApprove").IsEffectivelyVisible);
    });
}
