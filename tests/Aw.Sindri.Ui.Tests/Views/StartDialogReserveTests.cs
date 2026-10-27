using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Start dialog's layout does not move when an error shows, and each control of the Item row has a caption of its own (UX-pass C-9).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StartDialogReserveTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open()
    {
        var (window, shell) = await Connected(width: 1400, height: 900, ready: s => s.Workflows.Groups.Count > 0 && s.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Workflows);
        Settle(window);
        shell.OpenStart();
        await Until(window, () => shell.Start.Recipes.Count > 0 && !shell.Start.IsLoading);
        return (window, shell);
    }

    private static double Top(Window window, string name) => Named<Control>(window, name).TranslatePoint(new Point(0, 0), window)!.Value.Y; // why: the dialog is in the tree

    [Fact]
    public Task An_empty_submit_leaves_the_buttons_where_they_were() => OnUi(async () =>
    {
        var (window, shell) = await Open();
        var before = Top(window, "StartButton");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => shell.Start.HasError);

        Assert.Equal(before, Top(window, "StartButton"));
    });

    [Fact]
    public Task The_item_box_and_the_item_list_each_have_a_caption_of_their_own() => OnUi(async () =>
    {
        var (window, _) = await Open();

        var captions = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains(Labels.StartItem, captions, StringComparer.Ordinal);
        Assert.Contains(Labels.StartPickCaption, captions, StringComparer.Ordinal);
        Assert.Equal(Labels.StartPickCaption, AutomationProperties.GetName(Named<ComboBox>(window, "StartItemPick")));
    });
}
