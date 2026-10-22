using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Create item form as a modal dialog (UI spec §4.5, §8, §11.2; L10): counted as an overlay, Tab cycles in its card,
/// focus returns to the opener, the title says it is required and a blank submit says so under the field.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CreateItemDialogTests : IDisposable
{
    private static readonly RawInputModifiers _command = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CreateItemDialogTests).Assembly)
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

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        await Until(window, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.Groups.Count > 0 && shell.Health.Catalog.Controls.Count > 0);
        shell.GoTo(PageId.Work);
        Settle(window);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task Until(Window window, Func<bool> condition, string what = "the view never reached the expected state")
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Settle(window);
        Assert.True(condition(), what);
    }

    private static T Named<T>(Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static Control? Focused(Window window) => window.FocusManager?.GetFocusedElement() as Control;

    private static bool Inside(Window window)
    {
        var card = Named<Control>(window, "CreateCard");
        return Focused(window) is { } focused && (focused == card || card.IsVisualAncestorOf(focused));
    }

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None, string? symbol = null)
    {
        window.KeyPress(key, modifiers, physical, symbol);
        Settle(window);
    }

    // `C` from the "Create item" button on the Queue page: the form opens, with focus in its title.
    private static async Task<Button> OpenFromButton(Window window, ShellViewModel shell)
    {
        var opener = Named<Button>(window, "CreateButton");
        Assert.True(opener.Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
        await Until(window, () => shell.Create.IsOpen && Focused(window) == Named<TextBox>(window, "TitleField"));
        return opener;
    }

    [Fact]
    public Task The_form_is_a_dialog_with_a_required_title_and_an_enabled_Create() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        _ = await OpenFromButton(window, shell);

        var card = Named<Border>(window, "CreateCard");
        Assert.Equal(("Create item", AutomationControlType.Window), (AutomationProperties.GetName(card), AutomationProperties.GetControlTypeOverride(card)));
        Assert.Equal("Title (required)", AutomationProperties.GetName(Named<TextBox>(window, "TitleField")));
        Assert.True(Named<Button>(Named<Control>(window, "CreateControl"), "RunButton").IsEffectivelyEnabled);
    });

    [Fact]
    public Task The_dialog_card_is_480_wide_on_Surface2() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        _ = await OpenFromButton(window, shell);

        var card = Named<Border>(window, "CreateCard");
        var surface2 = Assert.IsType<Avalonia.Media.ISolidColorBrush>(window.FindResource("Brush.Surface2"), exactMatch: false);
        var background = Assert.IsType<Avalonia.Media.ISolidColorBrush>(card.Background, exactMatch: false);
        Assert.Equal((480d, surface2.Color), (card.Width, background.Color));
    });

    // F6, Ctrl+K, Ctrl+, and ? are swallowed while the form is open, as they are over the palette and the ? sheet.
    [Fact]
    public Task The_form_is_modal_F6_Ctrl_K_Ctrl_comma_and_question_mark_do_nothing() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = await OpenFromButton(window, shell);
        Assert.True(Named<Button>(window, "CancelCreate").Focus(NavigationMethod.Tab));   // not a text field, where ? is a character
        var page = shell.CurrentPage;

        Press(window, Key.F6, PhysicalKey.F6);
        Assert.True(Inside(window), "F6 stays in the card");
        Press(window, Key.F6, PhysicalKey.F6, RawInputModifiers.Shift);
        Assert.True(Inside(window), "Shift+F6 stays in the card");
        Press(window, Key.K, PhysicalKey.K, _command, "k");
        Press(window, Key.OemComma, PhysicalKey.Comma, _command, ",");
        Press(window, Key.OemQuestion, PhysicalKey.Slash, RawInputModifiers.Shift, "?");

        Assert.Equal((true, false, false, page), (shell.Create.IsOpen, shell.Palette.IsOpen, shell.Shortcuts.IsOpen, shell.CurrentPage));
        Assert.True(Inside(window));
    });

    [Fact]
    public Task Tab_cycles_inside_the_card_in_both_directions() => OnUi(async () =>
    {
        var (window, shell) = await OpenedForm();
        var title = Named<TextBox>(window, "TitleField");
        var seen = new List<Control?>();

        for (var i = 0; i < 12; i++)
        {
            Press(window, Key.Tab, PhysicalKey.Tab);
            Assert.True(Inside(window), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Tab {i + 1} left the card"));
            seen.Add(Focused(window));
        }

        Assert.Contains(title, seen);   // the cycle came round to the title again
        Assert.Contains(Named<Button>(window, "CancelCreate"), seen);

        Assert.True(title.Focus(NavigationMethod.Tab));
        Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.True(Inside(window), "Shift+Tab from the first field stays in the card");
        Assert.True(shell.Create.IsOpen);
    });

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OpenedForm()
    {
        var (window, shell) = await Connected();
        _ = await OpenFromButton(window, shell);
        return (window, shell);
    }

    // Esc, Cancel and the form closing by itself once the outcome has shown all give focus back to what opened it.
    [Fact]
    public Task Escape_returns_focus_to_the_opener() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var opener = await OpenFromButton(window, shell);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.Equal((false, opener), (shell.Create.IsOpen, Focused(window)));
    });

    [Fact]
    public Task Cancel_returns_focus_to_the_opener() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var opener = await OpenFromButton(window, shell);

        Named<Button>(window, "CancelCreate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Equal((false, opener), (shell.Create.IsOpen, Focused(window)));
    });

    [Fact]
    public Task Closing_by_itself_after_the_outcome_returns_focus_to_the_opener() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var opener = await OpenFromButton(window, shell);
        shell.Create.Title = "Proof: appears live";

        Named<Button>(Named<Control>(window, "CreateControl"), "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Create.CreatedId.Length > 0);
        _clock.Advance(ControlViewModel.AcceptedFor);
        Settle(window);

        Assert.Equal((false, opener), (shell.Create.IsOpen, Focused(window)));
    });

    // With no opener to return to (what had focus has gone), the "Create item" button gets it.
    [Fact]
    public Task Focus_falls_back_to_the_Create_button_when_nothing_had_it() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var gone = Named<Button>(window, "TitleButton");
        Assert.True(gone.Focus(NavigationMethod.Tab));

        shell.Create.Open();
        Settle(window);
        gone.IsEnabled = false;   // what opened the form is no longer there to return to
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.Equal((false, Named<Button>(window, "CreateButton")), (shell.Create.IsOpen, Focused(window)));
    });

    // L10 (a11y-5): a blank submit says "Title is required" under the field, speaks it, moves focus to the title and sends nothing.
    [Fact]
    public Task A_blank_submit_shows_the_message_under_the_field_and_moves_focus_to_the_title() => OnUi(async () =>
    {
        var (window, shell) = await OpenedForm();
        Assert.True(Named<Button>(window, "CancelCreate").Focus(NavigationMethod.Tab));

        Named<Button>(Named<Control>(window, "CreateControl"), "RunButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        var error = Named<TextBlock>(window, "TitleError");
        var live = Named<TextBlock>(window, "TitleLive");
        Assert.Equal((true, "Title is required"), (error.IsEffectivelyVisible, error.Text));
        Assert.Equal(("Title is required", true), (live.Text, live.Classes.Contains("live")));
        Assert.Equal(Named<TextBox>(window, "TitleField"), Focused(window));
        Assert.Equal("Title is required", AutomationProperties.GetHelpText(Named<TextBox>(window, "TitleField")));   // a11y-8: the error is attached to the field
        Assert.Empty(_server.Created);
        Assert.True(shell.Create.IsOpen);
    });

    // R5: the palette's CreateItem entry closes the palette and opens the form, through the shell: focus moves once, into the
    // form's title (the palette's opener goes on to the form, not back to the page first), and Esc returns to that opener.
    [Fact]
    public Task The_palettes_Create_item_entry_opens_the_form_with_one_focus_move_into_its_title() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        var opener = Named<Button>(window, "CreateButton");
        Assert.True(opener.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.K, _command, PhysicalKey.K, "k");
        Settle(window);
        Assert.True(shell.Palette.IsOpen);
        var entry = shell.Palette.Entries.Single(e => e.Id == "CreateItem");
        var moved = new List<string>();
        window.AddHandler(InputElement.GotFocusEvent, (_, e) => moved.Add((e.Source as Control)?.Name ?? "<unnamed>"));

        await shell.Palette.Run(entry, Token);
        await Until(window, () => shell.Create.IsOpen && Focused(window)?.Name == "TitleField");

        Assert.Equal((false, true), (shell.Palette.IsOpen, shell.Create.IsOpen));
        Assert.True(Inside(window));
        Assert.Equal(["TitleField"], moved);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);
        Assert.Equal((false, opener), (shell.Create.IsOpen, Focused(window)));
    });
}
