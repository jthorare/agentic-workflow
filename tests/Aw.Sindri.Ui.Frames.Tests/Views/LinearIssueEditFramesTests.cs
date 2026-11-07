using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Linear issue sheet's edit form in the window at 520 px (Linear hub spec §3): Save stays in view under a scrolling form, the conflict banner and the write-access banner.</summary>
[Lane(TestLane.Integration)]
public sealed class LinearIssueEditFramesTests : FrameHost
{
    private async Task<(Aw.Sindri.Ui.Views.Shell.ShellWindow Window, Aw.Sindri.Ui.ViewModels.Shell.ShellViewModel Shell)> Opened()
    {
        var (window, shell) = await Connected(width: 1200, height: 560);
        shell.ResizeSheet(520);
        Assert.True(DeepLink.TryParse("linear/ENG-12", out var link));
        shell.Navigate(link);
        await Until(window, () => shell.IssueSheet.Phase == FramePhase.Shown && Visible(window, "IssueBody"), "the issue sheet never showed the issue");
        return (window, shell);
    }

    [Fact]
    public Task The_editable_sheet_keeps_Save_in_view_at_520_px_while_the_form_scrolls() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        await Until(window, () => shell.IssueSheet.CanEdit && Visible(window, "IssueSave"));
        shell.IssueSheet.TitleField.Text = "A title long enough to need a second look";
        Settle(window);

        var viewport = Named<ScrollViewer>(window, "IssueSheetScroll");
        var save = Named<Button>(window, "IssueSave");
        var bottomRight = save.TranslatePoint(new Point(save.Bounds.Width, save.Bounds.Height), viewport)!.Value;   // why: the button is in the tree
        Assert.InRange(bottomRight.Y, 0, viewport.Bounds.Height);
        Assert.InRange(bottomRight.X, 0, viewport.Bounds.Width);
        Assert.True(save.IsEnabled);
        Assert.True(Named<ScrollViewer>(window, "IssueFormScroll").Extent.Height > 0);
        Assert.Equal("1 change: Title", AutomationProperties.GetName(Named<Border>(window, "IssueDirty")));
        Frame(window, "linear-issue-edit", "l1c-");
    });

    [Fact]
    public Task The_grid_runs_state_priority_assignee_cycle_labels_project_in_two_columns_above_the_description() => OnUi(async () =>
    {
        var (window, _) = await Opened();

        var fields = Named<ItemsControl>(window, "IssueEditFields");
        var cells = fields.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Name == "IssueEditField").ToList();
        Assert.Equal(6, cells.Count);
        var left = cells.Select(c => c.TranslatePoint(default, window)!.Value).ToList();
        Assert.Equal([true, false, true, false, true, false], left.Select(p => p.X < left[0].X + 10));   // two columns: odd cells on the left
        Assert.True(left[5].Y < Named<TextBox>(window, "IssueDescriptionBox").TranslatePoint(default, window)!.Value.Y);
    });

    [Fact]
    public Task A_409_shows_the_conflict_banner_with_Linears_value_beside_mine_and_focus_on_it() => OnUi(async () =>
    {
        Server.QueueLinearChange(RouteAnswer.Problem("linear-change", "conflict"));
        var (window, shell) = await Opened();
        await Until(window, () => shell.IssueSheet.CanEdit);
        var state = shell.IssueSheet.EditFields[0];
        state.Selected = state.Options.Single(o => o.Name == "In Review");

        await shell.IssueSheet.Save(Token);
        await Until(window, () => Visible(window, "IssueConflict"));

        var banner = Named<Border>(window, "IssueConflict");
        Assert.Equal(("State", "Linear: Done · yours: In Review"), (shell.IssueSheet.ConflictRows[0].Label, shell.IssueSheet.ConflictRows[0].Text));
        Assert.InRange(banner.TranslatePoint(new Point(banner.Bounds.Width, 0), window)!.Value.X, 0, window.Width);   // why: the banner is in the tree
        Assert.True(Visible(window, "IssueUseLinearsAll") && Visible(window, "IssueSaveMine") && Visible(window, "IssueUseLinears"));
        await Until(window, () => banner.IsFocused, "focus never reached the conflict banner");
        Frame(window, "linear-issue-conflict", "l1c-");
    });

    [Fact]
    public Task Without_a_write_key_the_first_control_is_the_Connect_button_and_the_form_is_text() => OnUi(async () =>
    {
        Server.LinearIssueAnswer = RouteAnswer.Api("linear-issue", "no-write-key");
        var (window, _) = await Opened();

        var sheet = Named<Control>(window, "IssueSheet");
        var first = sheet.GetVisualDescendants().OfType<InputElement>().First(c => c.Focusable && c.IsTabStop && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
        Assert.Equal("IssueConnect", (first as Control)?.Name);
        Assert.False(Visible(window, "IssueSave"));
        Assert.False(Visible(window, "IssueEditFields"));
        Frame(window, "linear-issue-write-access", "l1c-");
    });

    [Fact]
    public Task Every_control_of_the_edit_form_has_an_accessible_name() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        await Until(window, () => shell.IssueSheet.CanEdit && Visible(window, "IssueSave"));

        var sheet = Named<Control>(window, "IssueSheet");
        var controls = sheet.GetVisualDescendants().OfType<Control>().Where(c => c is Button or TextBox or ComboBox or CheckBox && c.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(controls);
        Assert.All(controls, c => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)), c.Name));
    });
}
