using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The window's size, the sheet's width and the last page as local preferences (UI spec §3.2, §12.3).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellPreferencesTests
{
    private readonly InMemoryPreferencesStore _store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(_store), new FakeClipboard());
        await shell.Restore(Token);
        return shell;
    }

    private Task<UiPreferences> Stored() => new Preferences(_store).Load(Token).AsTask();

    [Fact]
    public async Task A_first_run_opens_Home_with_the_default_sheet_and_no_bounds()
    {
        var shell = await Shell();

        Assert.Equal((PageId.Home, null, UiPreferences.DefaultSheetWidth), (shell.CurrentPage, shell.Window, shell.SheetWidth));
        Assert.Equal(0, _store.Writes);
    }

    [Fact]
    public async Task The_window_bounds_are_remembered_and_grown_to_the_minimum()
    {
        (await Shell()).RememberWindow(new WindowBounds(10, 20, 640, 480));

        Assert.Equal(new WindowBounds(10, 20, 1000, 600), (await Stored()).Window);
        Assert.Equal(new WindowBounds(10, 20, 1000, 600), (await Shell()).Window);
    }

    [Fact]
    public async Task The_sheet_width_is_remembered_within_420_to_760()
    {
        var shell = await Shell();

        shell.ResizeSheet(600);
        Assert.Equal(600, (await Stored()).SheetWidth);

        shell.ResizeSheet(2000);
        Assert.Equal(UiPreferences.MaxSheetWidth, shell.SheetWidth);
        Assert.Equal(UiPreferences.MaxSheetWidth, (await Shell()).SheetWidth);
    }

    [Fact]
    public async Task Every_change_writes_the_whole_file_keeping_the_others()
    {
        var shell = await Shell();

        shell.GoTo(PageId.System);
        shell.ResizeSheet(500);
        shell.RememberWindow(new WindowBounds(0, 0, 1280, 800));

        var stored = await Stored();
        Assert.Equal(("System", 500d, new WindowBounds(0, 0, 1280, 800)), (stored.LastPage, stored.SheetWidth, stored.Window));
        Assert.Equal(3, _store.Writes);
    }

    [Fact]
    public async Task A_shell_write_keeps_what_Settings_wrote_since_start_up()
    {
        var shell = await Shell();
        await new Preferences(_store).Update(_ => UiPreferences.Defaults with { TextScale = 1.2, ReduceMotion = true });

        shell.ResizeSheet(600);

        var stored = await Stored();
        Assert.Equal((1.2, true, 600d), (stored.TextScale, stored.ReduceMotion, stored.SheetWidth));
    }

    // Settings and the shell write through one queue: each reads the file only after the other's write, so neither
    // writes back what it read before the other changed it.
    [Fact]
    public async Task A_Settings_change_and_a_shell_change_made_together_both_land()
    {
        var store = new GatedPreferencesStore();
        var preferences = new Preferences(store);
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), preferences, new FakeClipboard());
        await shell.Restore(Token);
        using var settings = new SettingsViewModel(preferences, new FakeMotionPreference(), _ => { });
        store.HoldReads();

        settings.ReduceMotion = true;
        shell.ResizeSheet(600);
        store.ReleaseReads();
        await preferences.Flush();

        var stored = await preferences.Load(Token);
        Assert.Equal((true, 600d), (stored.ReduceMotion, stored.SheetWidth));
    }

    [Fact]
    public async Task A_page_already_shown_is_not_written_again()
    {
        var shell = await Shell();

        shell.GoTo(PageId.Home);

        Assert.Equal(0, _store.Writes);
    }
    // §12.3 paletteHistory: each entry run from the palette is remembered, a repeat moving to the newest end.
    [Fact]
    public async Task Palette_entries_run_are_remembered_newest_last()
    {
        var shell = await Shell();
        shell.Palette.Open();

        await shell.Palette.Run(shell.Palette.Entries.First(e => e.Id == "go:Work"), Token);
        shell.Palette.Open();
        await shell.Palette.Run(shell.Palette.Entries.First(e => e.Id == "go:Recipes"), Token);
        shell.Palette.Open();
        await shell.Palette.Run(shell.Palette.Entries.First(e => e.Id == "go:Work"), Token);

        Assert.Equal(["go:Recipes", "go:Work"], (await Stored()).PaletteHistory, StringComparer.Ordinal);
        Assert.Equal(PageId.Work, shell.CurrentPage);
    }
}
