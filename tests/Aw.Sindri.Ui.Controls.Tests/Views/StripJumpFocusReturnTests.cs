using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Shell;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Enter on a strip row with no park row jumps to Workflows with the instance's sheet (#1360); Esc there puts focus on the instance's own frame (its board card, or its list row's Open), never the Board/List toggle or the page.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StripJumpFocusReturnTests : FrameHost
{
    [Fact]
    public Task Esc_after_a_no_park_strip_rows_jump_puts_focus_on_its_board_card() => OnUi(async () =>
    {
        var (window, address) = await JumpAndClose(WorkflowViewMode.Board);

        var card = window.GetVisualDescendants().OfType<RunCard>().FirstOrDefault(c => c.IsEffectivelyVisible && c.Address == address);
        Assert.NotNull(card);
        var frame = card.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "WorkflowCardFrame");
        Assert.NotNull(frame);
        Assert.Same(frame, window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task Esc_after_a_no_park_strip_rows_jump_puts_focus_on_its_list_row() => OnUi(async () =>
    {
        var (window, address) = await JumpAndClose(WorkflowViewMode.List);

        var row = window.GetVisualDescendants().OfType<InstanceRow>().FirstOrDefault(r => r.IsEffectivelyVisible && r.Address == address);
        Assert.NotNull(row);
        var open = row.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "WorkflowRowOpen");
        Assert.NotNull(open);
        Assert.Same(open, window.FocusManager?.GetFocusedElement());
    });

    // Enter on a strip row no park row of Needs you points at (the same match ShellViewModel.ParkRowFor makes), then Esc on the sheet it opens on Workflows.
    private async Task<(ShellWindow Window, string Address)> JumpAndClose(WorkflowViewMode mode)
    {
        var (window, shell) = await Connected(s => s.Strip.Rows.Count > 0);
        shell.Workflows.SetViewMode(mode);

        var parks = shell.NeedsYou.Seen.Rows.Where(r => string.Equals(r.Entry.Kind, NeedsYouKinds.Park, StringComparison.Ordinal)).ToList();
        var strip = shell.Strip.Rows.First(r => !parks.Exists(p =>
            (p.Entry.Subject is { } s && (string.Equals(s.Id, r.Address, StringComparison.Ordinal) || string.Equals(s.Id, r.InstanceId, StringComparison.Ordinal)))
            || (r.ItemId is { } item && string.Equals(p.Entry.ItemId, item, StringComparison.Ordinal))));
        var address = strip.Address;
        shell.SelectStripRow(address);
        Assert.Same(strip, shell.Strip.SelectedRow);

        Assert.True(shell.Press("Enter"));
        await Until(window, () => shell.IsWorkflows && shell.IsInstanceSheet && shell.IsSheetShown);   // the jump to Workflows, not a sheet over Home
        Assert.Equal(address, shell.Instance.Address);

        shell.CloseSheet();
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        return (window, address);
    }
}
