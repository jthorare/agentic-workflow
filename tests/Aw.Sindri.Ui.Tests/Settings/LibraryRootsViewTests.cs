using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.Views;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>Settings' Library card under the headless platform (review page spec §6.3, §6.4): its words, its screen-reader names, what an agent session sees, and the form's field errors.</summary>
[Lane(TestLane.Integration)]
public sealed class LibraryRootsViewTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    private readonly List<Window> _windows = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(LibraryRootsViewTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private Window ShowSettings(LibraryRootsViewModel card)
    {
        using var settings = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _ => { }) { Library = card };
        var window = new Window { Width = 900, Height = 900, Content = new SettingsView { DataContext = settings } };
        _windows.Add(window);
        window.Show();
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name)
        where T : Control => window.GetLogicalDescendants().OfType<T>().Single(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    private LibraryRootsViewModel Card() => new(() => _api, _server, new FakeFolderPicker(), _clock);

    private async Task<LibraryRootsViewModel> Read(string scenario)
    {
        _server.LibraryAnswer = RouteAnswer.Api("library", scenario);
        var card = Card();
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public Task The_card_is_a_level_two_heading_with_its_intro_and_says_it_is_reading_before_the_first_answer() => OnUi(() =>
    {
        var window = ShowSettings(Card());

        Assert.Equal(("Library", true, Labels.LibraryFoldersReading), (Find<TextBlock>(window, "LibraryTitle").Text, Find<TextBlock>(window, "LibraryReading").IsEffectivelyVisible, Find<TextBlock>(window, "LibraryReading").Text));
        Assert.Equal(Labels.LibraryFoldersIntro, Find<TextBlock>(window, "LibraryIntro").Text);
        Assert.False(Find<Border>(window, "LibraryFoldersCard").IsEffectivelyVisible);
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_folders_card_lists_every_root_with_screen_reader_names_and_offers_Add_folder() => OnUi(async () =>
    {
        var card = await Read("ok");
        var window = ShowSettings(card);

        Assert.True(Find<Border>(window, "LibraryFoldersCard").IsEffectivelyVisible);
        var rowNames = window.GetLogicalDescendants().OfType<Grid>().Select(g => AutomationProperties.GetName(g) ?? string.Empty).Where(n => n.Contains("scanned", StringComparison.Ordinal) || n.Contains("Reading", StringComparison.Ordinal));
        Assert.Equal(card.Rows.Select(r => r.AutomationName), rowNames, StringComparer.Ordinal);
        var add = Find<Button>(window, "LibraryAdd");
        Assert.Equal((true, Labels.LibraryFoldersAdd), (add.IsEffectivelyVisible, AutomationProperties.GetName(add)));
        var buttons = window.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => AutomationProperties.GetName(b) ?? string.Empty).ToList();
        Assert.Contains("Turn off aw", buttons, StringComparer.Ordinal);
        Assert.Contains("Remove work", buttons, StringComparer.Ordinal);
        Assert.Contains("Remove inbox", buttons, StringComparer.Ordinal);
        Assert.DoesNotContain(buttons, n => n.Contains("repo:", StringComparison.Ordinal));
    });

    [Fact]
    public Task An_agent_session_sees_the_empty_words_and_no_Add_folder_button() => OnUi(async () =>
    {
        var window = ShowSettings(await Read("session"));

        Assert.Equal((true, Labels.LibraryFoldersNone, false), (Find<Border>(window, "LibraryEmptyCard").IsEffectivelyVisible, Find<TextBlock>(window, "LibraryEmpty").Text, Find<Button>(window, "LibraryAdd").IsEffectivelyVisible));
        Assert.False(Find<Border>(window, "LibraryForm").IsEffectivelyVisible);
    });

    [Fact]
    public Task Add_folder_shows_the_form_on_its_fields_and_a_refusal_shows_under_the_field_it_names() => OnUi(async () =>
    {
        var card = await Read("ok");
        var window = ShowSettings(card);

        card.OpenForm();
        Settle(window);
        Assert.True(Find<Border>(window, "LibraryForm").IsEffectivelyVisible);
        Assert.Equal(("Name", "Folder", "Don't read"), (AutomationProperties.GetName(Find<TextBox>(window, "LibraryFormName")), AutomationProperties.GetName(Find<TextBox>(window, "LibraryFormPath")), AutomationProperties.GetName(Find<TextBox>(window, "LibraryFormDeny"))));
        Assert.Equal(Labels.LibraryFoldersSave, Find<Button>(window, "LibraryFormSave").Content);
        Assert.False(Find<Button>(window, "LibraryFormSave").IsEnabled);

        (card.Form.Name, card.Form.Path) = ("Work Notes", "~/Documents/Notes");
        _server.LibraryRootAnswer = RouteAnswer.Problem("library-roots", "bad-name");
        await card.Form.Save(Token);
        Settle(window);

        Assert.Equal((true, false, false), (Find<TextBlock>(window, "LibraryFormNameError").IsEffectivelyVisible, Find<TextBlock>(window, "LibraryFormPathError").IsEffectivelyVisible, Find<TextBlock>(window, "LibraryFormError").IsEffectivelyVisible));
        Assert.Contains("isn't a name", Find<TextBlock>(window, "LibraryFormNameError").Text, StringComparison.Ordinal);
        Assert.True(Find<Border>(window, "LibraryForm").IsEffectivelyVisible);
    });

    [Fact]
    public Task A_save_that_waits_for_the_approval_shows_the_waiting_line_and_closes_the_form() => OnUi(async () =>
    {
        var card = await Read("ok");
        var window = ShowSettings(card);
        card.OpenForm();
        (card.Form.Name, card.Form.Path) = ("harbor-notes", "~/Documents/Harbor");
        _server.LibraryRootAnswer = RouteAnswer.Api("library-roots", "added-pending-others");

        await card.Form.Save(Token);
        Settle(window);

        Assert.Equal((true, Labels.ReviewsLoginPending, false), (Find<TextBlock>(window, "LibraryNote").IsEffectivelyVisible, Find<TextBlock>(window, "LibraryNote").Text, Find<Border>(window, "LibraryForm").IsEffectivelyVisible));
    });
}
