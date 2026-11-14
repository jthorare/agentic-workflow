using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The G-chords' timing, gating and filter (screens spec §3, rev 7 §8): the wait is on the clock, not a thread-pool timer.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellChordTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        return shell;
    }

    // The wait ends on the clock's thread, so a test waits for the flag with a bound, never for a count of yields. The bound is
    // well below ChordWait (1 s): a wait that ran on a real timer would still be pending when it runs out.
    private static async Task Expired(ShellViewModel shell)
    {
        var bound = Stopwatch.StartNew();
        while (shell.IsChordPending && bound.Elapsed < _expiryBound)
        {
            await Task.Delay(2, Token);
        }
    }

    private static readonly TimeSpan _expiryBound = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task A_chord_just_inside_its_wait_still_takes_the_second_key()
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        _clock.Advance(ShellViewModel.ChordWait - TimeSpan.FromMilliseconds(1));

        Assert.True(shell.IsChordPending);
        Assert.True(shell.PressKey("A"));
        Assert.Equal(PageId.Agents, shell.CurrentPage);
    }

    [Fact]
    public async Task The_shortcuts_sheet_names_exactly_the_G_chords_that_exist()
    {
        const string prefix = "G then ";
        var line = Labels.FixedKeys.Single(k => k.Keys.StartsWith(prefix, StringComparison.Ordinal)).Keys;
        var named = line[prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        foreach (var key in "ABCDEFGHIJKLMNOPQRSTUVWXYZ.".Select(c => c.ToString()))
        {
            var shell = await Shell();
            _ = shell.PressKey("G");

            Assert.Equal(named.Contains(key), shell.PressKey(key));
        }
    }

    [Fact]
    public async Task The_shortcuts_sheet_names_no_cancel_or_take_over_key_nothing_binds()
    {
        // Shift+X (cancel the run), T and B (take over, hand back) have no binding yet (#2294): the sheet may name one only when
        // pressing it through the shell does something.
        string[] unbound = ["Shift+X", "T", "B"];
        var named = Labels.FixedKeys.SelectMany(k => k.Keys.Split(" / ", StringSplitOptions.RemoveEmptyEntries)).Where(unbound.Contains).ToList();
        var shell = await Shell();

        foreach (var key in named)
        {
            Assert.True(shell.Press(key), $"the shortcuts sheet names {key}, but pressing it does nothing");
        }
    }

    [Fact]
    public async Task A_chord_at_its_wait_has_expired()
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        _clock.Advance(ShellViewModel.ChordWait);
        await Expired(shell);

        Assert.False(shell.IsChordPending);
        Assert.Equal(string.Empty, shell.ChordHint);
        Assert.False(shell.PressKey("A"));
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact]
    public async Task An_old_wait_cannot_clear_a_newer_chord()
    {
        var shell = await Shell();
        _ = shell.PressKey("G");
        _clock.Advance(TimeSpan.FromMilliseconds(900));
        _ = shell.PressKey("A");
        _ = shell.PressKey("G");

        _clock.Advance(TimeSpan.FromMilliseconds(200));   // 1.1 s after the first G, 0.2 s after the second
        await Task.Delay(50, Token);

        Assert.True(shell.IsChordPending);
        Assert.True(shell.PressKey("Q"));
        Assert.Equal(PageId.Work, shell.CurrentPage);
    }

    [Fact]
    public async Task Cancelling_a_waiting_chord_clears_it_and_the_next_key_is_not_its_second()
    {
        var shell = await Shell();
        _ = shell.PressKey("G");

        shell.CancelChord();

        Assert.False(shell.IsChordPending);
        Assert.False(shell.PressKey("A"));
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact]
    public async Task Cancelling_with_no_chord_waiting_does_nothing()
    {
        var shell = await Shell();

        shell.CancelChord();

        Assert.False(shell.IsChordPending);
    }

    [Theory]
    [InlineData(PageId.Work)]
    [InlineData(PageId.Home)]
    [InlineData(PageId.Settings)]
    internal async Task Any_navigation_but_G_D_clears_the_Home_filter(PageId page)
    {
        var shell = await Shell();
        _ = shell.PressKey("G");
        _ = shell.PressKey("D");
        Assert.Equal(HomeFilter.Decisions, shell.HomeFilter);

        shell.GoTo(page);

        Assert.Equal(HomeFilter.All, shell.HomeFilter);
    }

    // GoTo(page, filter) sets the filter once: G D with the filter already Decisions raises no HomeFilter change at all
    // (it is not cleared and set again), and from All it changes once (corr-3).
    [Fact]
    public async Task G_D_sets_the_Home_filter_once_and_a_repeat_changes_nothing()
    {
        var shell = await Shell();
        var changes = 0;
        shell.PropertyChanged += (_, e) => changes += string.Equals(e.PropertyName, nameof(ShellViewModel.HomeFilter), StringComparison.Ordinal) ? 1 : 0;

        _ = shell.PressKey("G");
        _ = shell.PressKey("D");
        Assert.Equal(1, changes);
        _ = shell.PressKey("G");
        _ = shell.PressKey("D");

        Assert.Equal(1, changes);
        Assert.Equal(HomeFilter.Decisions, shell.HomeFilter);
    }

    [Fact]
    public async Task GoTo_with_a_filter_shows_the_page_with_that_filter()
    {
        var shell = await Shell();

        shell.GoTo(PageId.Home, HomeFilter.Decisions);

        Assert.Equal((PageId.Home, HomeFilter.Decisions), (shell.CurrentPage, shell.HomeFilter));
    }

    [Fact]
    public async Task G_D_on_Home_with_the_filter_already_set_keeps_it()
    {
        var shell = await Shell();
        _ = shell.PressKey("G");
        _ = shell.PressKey("D");

        _ = shell.PressKey("G");
        _ = shell.PressKey("D");

        Assert.Equal(HomeFilter.Decisions, shell.HomeFilter);
    }

    [Fact]
    public async Task Chords_navigate_while_Sindri_is_not_running()
    {
        var shell = await Shell();
        shell.GoTo(PageId.Context);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.NotRunning; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal(ShellState.NotRunning, shell.State);
        Assert.True(shell.PressKey("G"));
        Assert.True(shell.IsChordPending);
        Assert.True(shell.PressKey("A"));
        Assert.Equal(PageId.Agents, shell.CurrentPage);
    }
}
