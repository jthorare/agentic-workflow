using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Aw.Sindri.Ui.Views.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept rows as drawn (#3677, decision 1): one line each, Pin as a hover action and the p key.</summary>
[Lane(TestLane.Integration)]
public sealed class ConceptRowViewTests
{
    private static readonly MapRebuildView _idle = new(MapRebuildPhases.Idle, Progress: null, RetryAt: null, Stale: false, Failed: null);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Window Window, ConceptLayerView View, ConceptLayerViewModel Layer, List<PinSheetOpen> Opened) Show()
    {
        List<PinSheetOpen> opened = [];
        var layer = new ConceptLayerViewModel(_ => { });
        layer.WirePins(opened.Add, () => "api", TimeZoneInfo.Utc);
        layer.Show(AConcepts.Ok(
        [
            AConcepts.Concept("concept:auth", "Auth", "Who may sign in, and how a session starts.", features: 4, records: 3, decisions: 2, code: 40),
            AConcepts.Concept("concept:billing", "Billing", "Invoices and payments.", features: 2, records: 1, decisions: 1, code: 12),
        ]));
        layer.ShowPins(new PinsView(1, "api", "file-h", "v1", [], _idle));
        var view = new ConceptLayerView { DataContext = layer };
        var window = new Window { Content = view, Width = 1280, Height = 700 };
        window.Show();
        Settle(window);
        return (window, view, layer, opened);
    }

    private static bool IsOpenButton(Button b) => b.DataContext is ConceptTileViewModel && AutomationProperties.GetAutomationId(b) is not ("PinThis" or "EditPin");

    [Fact]
    public async Task A_row_is_one_line_with_no_summary_or_breakdown_drawn_under_its_name()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ConceptRowViewTests).Assembly).Dispatch(() =>
        {
            var (window, view, _, _) = Show();
            try
            {
                var list = view.FindControl<ItemsControl>("ConceptList")!;
                var texts = list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();

                var height = list.ItemsPanelRoot!.Children[0].Bounds.Height;
                Assert.True(height <= 48, string.Create(CultureInfo.InvariantCulture, $"a concept row is {height} px tall"));
                Assert.DoesNotContain("Who may sign in, and how a session starts.", texts, StringComparer.Ordinal);
                Assert.DoesNotContain(texts, t => t is not null && t.Contains("records", StringComparison.Ordinal));
                Assert.Contains("40 files", texts, StringComparer.Ordinal);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_p_key_on_a_focused_row_opens_the_pin_sheet_for_that_concept()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ConceptRowViewTests).Assembly).Dispatch(() =>
        {
            var (window, view, _, opened) = Show();
            try
            {
                var row = view.GetVisualDescendants().OfType<Button>().First(IsOpenButton);
                _ = row.Focus(NavigationMethod.Tab);
                Settle(window);

                window.KeyPress(Key.P, RawInputModifiers.None, PhysicalKey.P, keySymbol: "p");
                Settle(window);

                var request = Assert.Single(opened);
                Assert.Equal("concept:auth", request.Card?.Id, StringComparer.Ordinal);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Pin_shows_while_the_pointer_is_over_a_row_and_not_otherwise()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ConceptRowViewTests).Assembly).Dispatch(() =>
        {
            var (window, view, _, _) = Show();
            try
            {
                var list = view.FindControl<ItemsControl>("ConceptList")!;
                List<Button> PinButtons() => [.. list.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(AutomationProperties.GetAutomationId(b), "PinThis", StringComparison.Ordinal))];
                var before = PinButtons().Count(b => b.IsEffectivelyVisible);

                var firstRow = list.ItemsPanelRoot!.Children[0];
                window.MouseMove(firstRow.TranslatePoint(new Point(40, 10), window) ?? default, RawInputModifiers.None);
                Settle(window);

                Assert.Equal(0, before);
                Assert.Equal(1, PinButtons().Count(b => b.IsEffectivelyVisible));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
