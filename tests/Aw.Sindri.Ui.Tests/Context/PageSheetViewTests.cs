using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using SectionView = Aw.Sindri.Ui.Views.Context.Whole.PagesSectionView;
using SheetView = Aw.Sindri.Ui.Views.Context.Whole.PageSheetView;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2: the section's buttons ask for the sheet, and the sheet starts with focus on Name (pages spec §3.6 "Keyboard").</summary>
[Lane(TestLane.Integration)]
public sealed class PageSheetViewTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task A_row_button_asks_for_its_sheet_and_the_sheet_starts_on_Name()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PageSheetViewTests).Assembly).Dispatch(async () =>
        {
            var writes = new FakePageWrites();
            writes.Add("surface:screen/week", new PageDraft(Id: "schedule-week", Name: "Schedule week", Repo: null, Surface: "surface:screen/week", Owner: false, Actions: []));
            var section = new PagesSectionViewModel { Writes = writes };
            section.Show(PagesSectionTests.Scheduling());
            var asked = new List<string?>();
            section.SheetRequested += (_, e) => asked.Add(e.Value);
            var sectionWindow = new Window { Content = new SectionView { DataContext = section }, Width = 600, Height = 700 };
            var sheet = new PageSheetViewModel(writes, [], [], openConcept: null);
            var sheetWindow = new Window { Content = new SheetView { DataContext = sheet }, Width = 600, Height = 700 };
            try
            {
                sectionWindow.Show();
                Settle(sectionWindow);
                var buttons = sectionWindow.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(b.Name, "PagesRowSheet", StringComparison.Ordinal)).ToList();
                Expect.Sequence(["Edit page", "Name this page", "Name this page"], buttons.Select(b => (string)b.Content!));
                buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                sectionWindow.GetVisualDescendants().OfType<Button>().First(b => string.Equals(b.Name, "PagesNewPage", StringComparison.Ordinal)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Expect.Sequence(["surface:screen/week", null], asked);

                sheetWindow.Show();
                await sheet.OpenPage("surface:screen/week", TestContext.Current.CancellationToken);
                Settle(sheetWindow);
                var focused = sheetWindow.FocusManager?.GetFocusedElement() as Control;
                Assert.Equal("PageSheetName", focused?.Name);
            }
            finally
            {
                sectionWindow.Close();
                sheetWindow.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
