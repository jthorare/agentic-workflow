using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The keys on Home's Workflows strip (screens §4.1): J / K between the panels, Enter, and A for the inline answer.</summary>
[Lane(TestLane.Unit)]
public sealed class StripKeysTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 2, WithYou: 0, Queued: 1) };

    private static InstanceList Listing(params InstanceSummary[] rows) => FixtureInstances.Root with { Instances = rows };

    private static InstanceSummary Where(Func<InstanceSummary, bool> predicate) => FixtureInstances.Rows.First(predicate);

    private static bool IsDeskAnswer(InstanceSummary row) =>
        row.Answer is null && row.Actions.Contains("CompleteStep", StringComparer.Ordinal) && row.States.Any(s => s.Desk && string.Equals(s.State, row.State, StringComparison.Ordinal));

    private static bool IsProtected(InstanceSummary row) => row.Answer is not null && string.Equals(row.Answer.Class, "protected", StringComparison.Ordinal);

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0, Token);

        Assert.Equal(ShellState.Normal, shell.State);
        return shell;
    }

    private static string Where(ShellViewModel shell) =>
        shell.NeedsYou.SelectedId is { } n ? $"needs:{n}"
        : shell.Strip.SelectedAddress is { } a ? $"strip:{a}"
        : shell.Queue.SelectedId is { } q ? $"queue:{q}"
        : "none";

    [Fact]
    public Task J_K_move_needs_you_then_strip_then_running_then_queued() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        shell.Strip.Update(FixtureInstances.Root);
        var strip = shell.Strip.Rows.Select(r => r.Address).ToList();
        var needsYou = shell.NeedsYou.Rows.Select(r => r.Id).ToList();
        var queue = shell.Queue.RunningRows.Concat(shell.Queue.QueuedRows).Select(r => r.Id).ToList();
        string[] forward = [.. needsYou.Select(i => $"needs:{i}"), .. strip.Select(a => $"strip:{a}"), $"queue:{queue[0]}", $"queue:{queue[1]}"];

        var seen = new List<string>();
        foreach (var _ in forward)
        {
            Assert.True(shell.Press("J"));
            seen.Add(Where(shell));
        }

        Expect.Sequence(forward, seen);

        var back = new List<string>();
        for (var i = forward.Length - 2; i >= 0; i--)
        {
            Assert.True(shell.Press("K"));
            back.Add(Where(shell));
        }

        Expect.Sequence(forward.Reverse().Skip(1), back);
    }, Token);

    [Fact]
    public Task K_from_the_first_queue_row_goes_to_the_strips_last_row_and_from_nothing_to_the_strip_when_needs_you_is_empty() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.Strip.Update(FixtureInstances.Root);

        Assert.True(shell.Press("J"));
        Assert.Equal($"strip:{shell.Strip.Rows[0].Address}", Where(shell));

        shell.Strip.Deselect();
        shell.Queue.Move(1, QueueScope.Home);
        Assert.True(shell.Queue.IsFirstOnHome);
        Assert.True(shell.Press("K"));
        Assert.Equal($"strip:{shell.Strip.Rows[^1].Address}", Where(shell));
    }, Token);

    [Fact]
    public Task Selecting_a_needs_you_or_queue_row_leaves_the_strip() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));
        shell.Strip.Update(FixtureInstances.Root);
        _ = shell.Strip.Select(shell.Strip.Rows[0].Address);

        shell.SelectNeedsYouRow(shell.NeedsYou.Rows[0].Id);

        Assert.Null(shell.Strip.SelectedAddress);
    }, Token);

    [Fact]
    public Task Enter_opens_workflow_address() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.Strip.Update(FixtureInstances.Root);
        _ = shell.Press("J");
        var address = shell.Strip.SelectedAddress;

        Assert.True(shell.Press("Enter"));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, address), shell.SheetLink);
        Assert.Equal(PageId.Workflows, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task A_on_a_strip_row_without_a_park_row_opens_its_workflow_link() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var row = Where(IsDeskAnswer);
        shell.Strip.Update(Listing(row));
        _ = shell.Press("J");

        Assert.True(shell.Press("A"));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, row.Address), shell.SheetLink);
    }, Token);

    [Fact]
    public Task A_never_answers_a_protected_row_and_does_nothing_on_a_row_without_a_ready_answer() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var locked = Where(IsProtected);
        var quiet = Where(r => !IsProtected(r) && !IsDeskAnswer(r) && r.Answer is null);
        shell.Strip.Update(Listing(locked, quiet));
        _ = shell.Press("J");

        Assert.Equal(locked.Address, shell.Strip.SelectedAddress);
        Assert.False(shell.Press("A"));
        Assert.Null(shell.SheetLink);

        _ = shell.Press("J");
        Assert.Equal(quiet.Address, shell.Strip.SelectedAddress);
        Assert.False(shell.Press("A"));
        Assert.Null(shell.SheetLink);
    }, Token);

    [Fact]
    public Task Answer_opens_the_park_rows_sheet_for_the_same_instance() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var row = Where(IsDeskAnswer);
        var park = NeedsYouRows.Park("park:" + row.Address) with { Subject = new NeedsYouSubject("workflow", row.Address) };
        shell.ShowNeedsYou(Feed(NeedsYouRows.Failure(), park));
        shell.Strip.Update(Listing(row));
        _ = shell.Strip.Select(row.Address);

        Assert.True(shell.Press("A"));

        // #1057: a desk step's answer form is the Needs you inbox's, so Answer opens the inbox on the park row, not a read-only sheet.
        Assert.True(shell.IsNeedsYouInbox);
        Assert.Null(shell.SheetLink);
        Assert.Equal(park.Id, shell.NeedsYou.SelectedId);
        Assert.Null(shell.Strip.SelectedAddress);
    }, Token);

    // review run 1, K18: the park row is found by its served kind, never by the words its chip happens to show.
    [Fact]
    public Task The_park_row_is_found_by_its_kind_and_not_by_its_chip_word() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var row = Where(IsDeskAnswer);
        var park = NeedsYouRows.Park("park:" + row.Address) with { Subject = new NeedsYouSubject("workflow", row.Address), Chip = new StateChip("WAITING ON YOU", ChipTone.Attn) };
        var lookalike = NeedsYouRows.Failure("failure:" + row.Address) with { Subject = new NeedsYouSubject("workflow", row.Address), Chip = new StateChip("PARKED", ChipTone.Attn) };
        shell.ShowNeedsYou(Feed(lookalike, park));
        shell.Strip.Update(Listing(row));
        _ = shell.Strip.Select(row.Address);

        Assert.True(shell.Press("A"));

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal(park.Id, shell.NeedsYou.SelectedId);
    }, Token);
}
