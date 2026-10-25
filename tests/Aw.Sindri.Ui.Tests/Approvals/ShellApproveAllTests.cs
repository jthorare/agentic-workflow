using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>The approve-all dialog in the shell: the setup row's button opens it, and nothing is approved by opening it.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellApproveAllTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new()
    {
        Catalog = RouteAnswer.SpecShaped("commands", "extra-approve-all-ready"),
        QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"),
        NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-setup-recipes"),
    };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.NeedsYou.Rows.Count > 0 && shell.Health.Catalog.Controls.Count > 0, Token);
        shell.GoTo(PageId.Home);
        return shell;
    }

    [Fact]
    public Task The_setup_row_offers_a_button_the_app_can_press_not_a_command_to_copy() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        var row = Assert.Single(shell.NeedsYou.Rows);
        var button = Assert.Single(row.Controls);

        Assert.Equal(("Approve all workflows", true, true), (button.Action.Label, button.IsPrimary, button.IsEnabled));
    }, Token);

    [Fact]
    public Task Pressing_it_opens_the_dialog_on_the_plan_and_sends_no_approval() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var action = Assert.Single(shell.NeedsYou.Rows).Controls[0].Action;

        shell.NeedsYou.Press(shell.NeedsYou.Rows[0].Id, action);
        await ShellWait.Until(shell, () => shell.ApproveAll.Phase == ApproveAllPhase.Plan && shell.ApproveAll.CanApprove, Token);

        Assert.Equal((true, 2), (shell.ApproveAll.IsOpen, shell.ApproveAll.Rows.Count));
        Assert.Empty(_server.Approvals);
    }, Token);

    [Fact]
    public Task The_key_that_runs_a_rows_primary_action_opens_it_too() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.NeedsYou.Select(shell.NeedsYou.Rows[0].Id);

        Assert.True(shell.NeedsYou.Press(NeedsYouKey.A));
        await ShellWait.Until(shell, () => shell.ApproveAll.IsOpen, Token);

        Assert.Empty(_server.Approvals);
    }, Token);
}
