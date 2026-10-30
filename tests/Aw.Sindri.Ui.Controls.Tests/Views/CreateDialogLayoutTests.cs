using System.Net;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>The Create dialog's layout and the sheet's dismiss name (proof B5, N1, N2, N7).</summary>
[Lane(TestLane.Integration)]
public sealed class CreateDialogLayoutTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CreateDialogLayoutTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Opened()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell, Width = 1400 };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && !(shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Health.Catalog.Controls.Count > 0); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        shell.GoTo(PageId.Work);
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
        Settle(window);
        Assert.True(shell.Create.IsOpen);
        return (window, shell);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Create(Window window) => Named<Button>(Named<Control>(window, "CreateControl"), "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task A_refusal_detail_wraps_inside_the_dialog() => OnUi(async () =>
    {
        _server.Create = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-CLI-003", "'jthorare' isn't a repo of the approved profile (fix: sindri profile approve, or name one of its repos, then try again with the repo spelled as the profile has it)");
        var (window, shell) = await Opened();
        shell.Create.Title = "A title";
        shell.Create.Repo = "jthorare";

        Create(window);
        for (var i = 0; i < 100 && !shell.Create.Control!.IsFailed; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(shell.Create.Control!.IsFailed);
        var card = Named<Border>(window, "CreateCard");
        var detail = Named<TextBlock>(window, "ErrorDetailText");
        var cardRight = card.TranslatePoint(new Point(card.Bounds.Width, 0), window)!.Value.X;
        var detailRight = detail.TranslatePoint(new Point(detail.Bounds.Width, 0), window)!.Value.X;
        Assert.True(detail.Bounds.Width > 0 && detail.IsEffectivelyVisible);
        Assert.True(detailRight <= cardRight, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the detail ends at {detailRight}, the card at {cardRight}"));
    });

    [Fact]
    public Task The_title_required_live_region_leaves_the_tree_once_a_title_is_typed() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        Assert.True(Named<TextBlock>(window, "TitleLive").IsVisible);   // in the tree before any submit: its state arrives into a region that exists
        Create(window);
        Settle(window);
        Assert.Equal("Title is required", Named<TextBlock>(window, "TitleLive").Text);

        shell.Create.Title = "Now there is one";
        Settle(window);

        Assert.False(Named<TextBlock>(window, "TitleLive").IsVisible);
        await Task.CompletedTask;
    });

    [Fact]
    public Task Create_and_Cancel_sit_on_one_row() => OnUi(async () =>
    {
        var (window, _) = await Opened();
        var run = Named<Button>(Named<Control>(window, "CreateControl"), "RunButton");
        var cancel = Named<Button>(window, "CancelCreate");

        var runY = run.TranslatePoint(new Point(0, run.Bounds.Height / 2), window)!.Value.Y;
        var cancelY = cancel.TranslatePoint(new Point(0, cancel.Bounds.Height / 2), window)!.Value.Y;

        Assert.InRange(Math.Abs(runY - cancelY), 0, 1.5);
        await Task.CompletedTask;
    });

    [Fact]
    public Task The_sheets_dismiss_button_says_what_it_closes() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        shell.Create.Close();
        shell.OpenItem(A.NewItem);
        Settle(window);

        Assert.Equal("Close sheet", AutomationProperties.GetName(Named<Button>(window, "SheetClose")));
        await Task.CompletedTask;
    });

    // Proof B6 (UI spec §4.6): the 404 state has Back, which closes the sheet like Esc and leaves focus on the page.
    [Fact]
    public Task The_not_found_state_has_a_Back_that_closes_the_sheet() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        shell.Create.Close();
        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.MissingItem));
        for (var i = 0; i < 100 && !shell.Item.IsNotFound; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        var back = Named<Button>(window, "NotFoundBack");
        Assert.Equal(("Back", true), (AutomationProperties.GetName(back), back.IsEffectivelyVisible));

        Assert.True(back.Focus(NavigationMethod.Tab));   // the keyboard user's Back has focus when it is pressed
        back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.False(shell.IsSheetShown);
        Assert.Same(Named<Border>(window, "Page"), window.FocusManager?.GetFocusedElement());   // a deep link has no opener row: the page region
    });

    // The opener path: the missing item opened from a row (J, Enter) gives focus back to that row.
    [Fact]
    public Task Back_from_a_missing_item_opened_from_a_row_returns_focus_to_that_row() => OnUi(async () =>
    {
        var (window, shell) = await Opened();
        shell.Create.Close();
        Settle(window);
        window.KeyPressQwerty(PhysicalKey.J, RawInputModifiers.None);
        Settle(window);
        var selected = shell.Queue.SelectedId!;
        _ = _server.Items.Remove(selected);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        for (var i = 0; i < 100 && !shell.Item.IsNotFound; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(shell.Item.IsNotFound);

        var back = Named<Button>(window, "NotFoundBack");
        Assert.True(back.Focus(NavigationMethod.Tab));
        back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        var focused = Assert.IsType<Border>(window.FocusManager?.GetFocusedElement());
        Assert.Equal(("RowFrame", selected), (focused.Name, Assert.IsType<ItemRowViewModel>(focused.DataContext).Id));
    });
}
