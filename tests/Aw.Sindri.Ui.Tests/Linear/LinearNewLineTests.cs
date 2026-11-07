using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>Home's "N new on your Linear issues" line and the tray's same line (Linear hub spec §3, "With #2076"): N is the since-you-looked strip's count.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearNewLineTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> ShellOnWork()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        shell.GoTo(PageId.Work);
        return shell;
    }

    [Fact]
    public Task Home_says_how_many_are_new_and_opens_Work_on_the_Linear_view() => UiThread.Run(async () =>
    {
        var shell = await ShellOnWork();
        await ShellWait.Until(shell, () => shell.Linear.NewCount == 3, Token);
        shell.GoTo(PageId.Home);

        Assert.Equal(("3 new on your Linear issues", true), (shell.LinearNewLine, shell.HasLinearNewLine));

        shell.OpenLinear();

        Assert.Equal((PageId.Work, true), (shell.CurrentPage, shell.Linear.IsLinear));
    }, Token);

    [Fact]
    public Task Home_has_the_line_without_Work_ever_being_opened() => UiThread.Run(async () =>
    {
        var shell = await ShellOnWork();
        shell.GoTo(PageId.Home);
        var fresh = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await fresh.Restore(Token);
        _ = fresh.Run(Token);

        await ShellWait.Until(fresh, () => fresh.LinearNewLine is not null, Token);

        Assert.Equal(("3 new on your Linear issues", PageId.Home), (fresh.LinearNewLine, fresh.CurrentPage));
    }, Token);

    [Fact]
    public Task Home_hides_the_line_at_zero() => UiThread.Run(async () =>
    {
        _server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");
        var shell = await ShellOnWork();
        await ShellWait.Until(shell, () => !shell.Linear.StripFailed && shell.Linear.Reading.IsCompleted, Token);

        Assert.Equal((null, false), (shell.LinearNewLine, shell.HasLinearNewLine));
    }, Token);

    [Fact]
    public Task Dismiss_takes_the_line_away() => UiThread.Run(async () =>
    {
        var shell = await ShellOnWork();
        await ShellWait.Until(shell, () => shell.Linear.NewCount == 3, Token);

        await shell.Linear.Dismiss();

        Assert.Null(shell.LinearNewLine);
    }, Token);

    [Fact]
    public Task The_tray_tooltip_gets_a_line_for_the_count() => UiThread.Run(async () =>
    {
        var shell = await ShellOnWork();

        await ShellWait.Until(shell, () => shell.Attention.Tray.Tooltip.EndsWith("\n3 new on your Linear issues", StringComparison.Ordinal), Token);
    }, Token);

    [Fact]
    public void The_summary_adds_the_line_to_the_tooltip_only_above_zero()
    {
        var with = AttentionSummary.For(feed: null, ShellState.Normal, A.Noon).WithLinearNew(3);
        var without = AttentionSummary.For(feed: null, ShellState.Normal, A.Noon).WithLinearNew(0);

        Assert.Equal((without.Tray.Tooltip + "\n3 new on your Linear issues", with.Badge, with.Title), (with.Tray.Tooltip, without.Badge, without.Title));
    }

    [Fact]
    public void One_reads_the_same_way() =>
        Assert.EndsWith("\n1 new on your Linear issues", AttentionSummary.For(feed: null, ShellState.Normal, A.Noon).WithLinearNew(1).Tray.Tooltip, StringComparison.Ordinal);
}
