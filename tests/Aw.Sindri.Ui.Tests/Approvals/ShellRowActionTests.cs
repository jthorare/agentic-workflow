using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Approvals;

/// <summary>Home's rows in the shell (gh-1038): a pressed action sends or says why it did not, never nothing; the approve-all dialog's Approve sends; Mark all seen marks every row.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellRowActionTests
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
    public Task The_approve_all_dialogs_approve_posts_the_approval_once() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.NeedsYou.Press(shell.NeedsYou.Rows[0].Id, shell.NeedsYou.Rows[0].Controls[0].Action);
        await ShellWait.Until(shell, () => shell.ApproveAll.Phase == ApproveAllPhase.Plan && shell.ApproveAll.CanApprove, Token);

        await shell.ApproveAll.Approve();

        _ = Assert.Single(_server.Approvals);
    }, Token);

    [Fact]
    public Task Mark_all_seen_marks_every_row_and_offers_the_undo() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.MarkAllSeen();

        Assert.All(shell.NeedsYou.Rows, row => Assert.False(row.HasNewWord));
        Assert.True(shell.NeedsYou.Seen.UndoOffered);
    }, Token);

    [Fact]
    public Task A_pressed_action_the_app_has_no_send_for_says_so_on_the_row() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var row = shell.NeedsYou.Rows[0];
        var other = new NeedsYouAction("SomethingNew", "Something new", ActionReadiness.Ready, "sindri something new", RejectLike: false);

        shell.NeedsYou.Press(row.Id, other);
        await ShellWait.Until(shell, () => row.HasNotice, Token);

        Assert.Equal(Labels.RowNotAvailable("sindri something new"), row.Notice);
    }, Token);
}
