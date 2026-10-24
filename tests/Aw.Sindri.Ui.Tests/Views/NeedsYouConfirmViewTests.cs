using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The inbox bar's approve button in a real window (needs-you slice 3; q93): the bar's primary approves with one click and no challenge, for a drifted profile and for approve-all.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouConfirmViewTests : FrameHost
{
    [Fact]
    public Task One_click_on_the_primary_approves_a_drifted_profile_with_no_challenge() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        shell.Inbox.Select("profile:drift");
        Settle(window);
        await Until(window, () => shell.Inbox.Bar.PrimaryEnabled, State(shell));
        Settle(window);

        Assert.True(Visible(window, "NeedsYouApprove"));
        Assert.Empty(Server.ProfileApprovals);   // showing it approved nothing

        Named<Button>(window, "NeedsYouApprove").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Inbox.Bar.IsApproved, $"profileApprovals={Server.ProfileApprovals.Count} refusal={shell.Inbox.Bar.Refusal}; {State(shell)}");

        _ = Assert.Single(Server.ProfileApprovals);
    });

    [Fact]
    public Task One_click_on_the_primary_approves_all_new_workflows() => OnUi(async () =>
    {
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready");
        Server.NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-recipes");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, 1440, 800);
        shell.OpenNeedsYouInbox();
        shell.Inbox.Select("setup:recipes");
        await Until(window, () => shell.Inbox.Bar.PrimaryEnabled, State(shell));
        Settle(window);
        Assert.Empty(Server.Approvals);

        Named<Button>(window, "NeedsYouApprove").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Inbox.Bar.IsApproved, $"approvals={Server.Approvals.Count} refusal={shell.Inbox.Bar.Refusal}; {State(shell)}");

        _ = Assert.Single(Server.Approvals);
    });

    private static string State(ShellViewModel shell) =>
        $"mode={shell.Inbox.Bar.Mode} selected={shell.NeedsYou.SelectedId} card={shell.Inbox.Card?.Id} primary={shell.Inbox.Bar.ShowsPrimary} enabled={shell.Inbox.Bar.Enabled}";
}
