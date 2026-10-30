using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Model picks table in the instance sheet (model-pick spec §6.1, accessibility): Enter on the Why sentence opens its flyout, Esc closes the flyout only and returns focus to the sentence, the next Esc closes the sheet; and no text runs past the sheet.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ModelPicksPanelTests : FrameHost
{
    private async Task<(Window Window, Button Why, ShellViewModel Shell)> Shown(int width = 1600)
    {
        var (window, shell) = await Connected(ready: null, width: width, height: 1000);
        Server.LaunchPicksAnswer = RouteAnswer.Api("launch-picks", "picked-with-usage");
        var detail = FixtureInstances.Detail("project-build");
        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.ModelPicks is not null);
        Named<Expander>(window, "InstanceModelPicksExpander").IsExpanded = true;
        Settle(window);
        var why = Named<Control>(window, "InstanceModelPickRows").GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible);
        return (window, why, shell);
    }

    [Fact]
    public Task Esc_closes_the_Why_flyout_only_and_returns_focus_to_the_sentence_then_the_next_Esc_closes_the_sheet() => OnUi(async () =>
    {
        var (window, why, shell) = await Shown();
        Assert.True(why.Focus(NavigationMethod.Tab));

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        await Until(window, () => why.Flyout?.IsOpen == true, "Enter did not open the Why flyout");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        await Until(window, () => why.Flyout?.IsOpen == false, "Esc did not close the flyout");
        Assert.True(shell.IsSheetShown);
        await Until(window, () => ReferenceEquals(window.FocusManager?.GetFocusedElement(), why), "focus did not return to the Why sentence");

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        await Until(window, () => !shell.IsSheetShown, "the next Esc did not close the sheet");
    });

    [Fact]
    public Task No_text_of_the_table_runs_past_the_sheet_at_the_narrow_width() => OnUi(async () =>
    {
        var (window, _, _) = await Shown(width: 1000);
        var sheet = Named<Control>(window, "InstanceModelPicks");
        var right = sheet.TranslatePoint(new Point(sheet.Bounds.Width, 0), window)!.Value.X;

        foreach (var text in sheet.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
        {
            var edge = text.TranslatePoint(new Point(text.Bounds.Width, 0), window)!.Value.X;
            Assert.True(edge <= right + 0.5, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"'{text.Text}' ends at {edge}, past the table's right edge {right}"));
            Assert.True(text.Bounds.Height >= text.DesiredSize.Height - 0.5, $"'{text.Text}' is clipped");
        }
    });
}
