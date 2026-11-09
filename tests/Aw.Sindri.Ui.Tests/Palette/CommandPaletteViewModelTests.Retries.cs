using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>The palette's announcements of a run it started (<see cref="CommandPaletteViewModelTests"/>, continued): retries, copies, and what is said once.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    private PaletteEntry SelectRebuild()
    {
        _palette.Open();
        for (var i = 0; i < 200 && _palette.Selected?.Id != "RebuildViews"; i++)
        {
            _palette.Move(1);
        }

        return _palette.Selected!;
    }

    // K1 (run 5, a11y-0): a retry with the same outcome inside the window is a new announcement. The text the live region holds is the
    // same as the last run's, so the palette rests the region on a neutral text first (the gate dark) and then says it.
    [Fact]
    public async Task A_retry_with_the_same_refusal_is_spoken_again()
    {
        _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        var rebuild = SelectRebuild();
        var spoken = Spoken();
        var live = new List<string>();
        _palette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.Announcement) && _palette.IsAnnouncing)
            {
                live.Add(_palette.Announcement);
            }
        };

        await _palette.Run(rebuild, Token);
        await _palette.Run(rebuild, Token);

        Assert.Equal(2, spoken.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.Equal(2, live.Count(t => t.Contains("SND-API-005: failed", StringComparison.Ordinal)));
        Assert.DoesNotContain("Command palette", live, StringComparer.Ordinal);   // the neutral text it rests on is never spoken: the region is dark for it
    }

    [Fact]
    public async Task A_retry_that_is_accepted_inside_the_two_seconds_is_spoken_again()
    {
        var rebuild = SelectRebuild();
        var spoken = Spoken();

        await _palette.Run(rebuild, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await _palette.Run(rebuild, Token);

        Assert.Equal(2, spoken.Count(t => t.StartsWith("Rebuild views: done", StringComparison.Ordinal)));
    }

    // K1 (a11y-2): a second Copy inside the 1.5 s says "Copied" again, from the palette; the control stays dark.
    [Fact]
    public async Task A_second_copy_inside_the_window_is_spoken_again_by_the_palette()
    {
        _palette.Open();
        _palette.Query = "set profile key";
        var entry = _palette.Selected;
        var spoken = Spoken();

        await _palette.Run(entry, Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await _palette.Run(entry, Token);

        Assert.Equal(2, spoken.Count(t => t.Contains("Copied", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_second_copy_inside_the_window_is_spoken_again_by_the_control()
    {
        var control = _controls.For("SetProfileKey")!;
        var said = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                said.Add(control.Announcement);
            }
        };

        await control.Copy(Token);
        _clock.Advance(TimeSpan.FromMilliseconds(500));
        await control.Copy(Token);

        Assert.Equal(2, said.Count(t => t == "Copied"));
    }

    // K3 (run 5, corr-1): "Sending…" is spoken once. When the selection moved off the entry after it was said, the outcome's own changes
    // (the outcome arrives before the phase does) must not say the entry's name and "Sending…" again.
    [Fact]
    public async Task Sending_is_spoken_once_when_the_selection_moved_off_after_it_was_said()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var rebuild = SelectRebuild();
        var spoken = Spoken();

        var run = _palette.Run(rebuild, Token);
        _clock.Advance(ControlViewModel.SendingAfter);
        _palette.Move(1);
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, spoken.Count(t => t.Contains("Sending…", StringComparison.Ordinal)));
        Assert.Contains(spoken, t => t.StartsWith("Rebuild views: done", StringComparison.Ordinal));
    }

    // K4 (run 5, corr-2): a send that is cancelled leaves the phase Idle, so the palette's finally really lets go of the control.
    [Fact]
    public async Task A_cancelled_palette_send_leaves_the_control_idle_and_unmuted()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var rebuild = SelectRebuild();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = _palette.Run(rebuild, stop.Token);
        await stop.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Equal((false, CommandPhase.Idle, true), (rebuild.Control!.IsMuted, rebuild.Control.Phase, rebuild.IsEnabled));
    }

    // Final pass: a send cancelled after "Sending…" was spoken goes dark before the phase goes Idle, so the palette never says "ready" for it.
    [Fact]
    public async Task A_palette_send_cancelled_after_sending_was_spoken_never_says_ready()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var rebuild = SelectRebuild();
        var spoken = Spoken();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = _palette.Run(rebuild, stop.Token);
        _clock.Advance(ControlViewModel.SendingAfter);
        await stop.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        Assert.Contains(spoken, t => t.Contains("Sending…", StringComparison.Ordinal));
        Assert.DoesNotContain(spoken, t => t.Contains("Rebuild views, ready", StringComparison.Ordinal));
    }

    // K2 (run 5, a11y-1 and corr-0): Esc, or the close-first policy, mid-send hands the outcome to a control whose only view is the stalled
    // banner, hidden unless stalled: it was announced 0 times. The shell's command-outcome region speaks it instead, once, and the control stays dark.
    private (CommandPaletteViewModel Palette, CommandOutcomeRegion Region, List<string> Said) Closing(bool bannerShowsControl)
    {
        var region = new CommandOutcomeRegion(_ => bannerShowsControl);
        var palette = new CommandPaletteViewModel(_controls, _pages.Add, _history.Add, region);
        var said = new List<string>();
        region.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandOutcomeRegion.Text) && region.IsLive)
            {
                said.Add(region.Text);
            }
        };
        return (palette, region, said);
    }

    private static PaletteEntry Find(CommandPaletteViewModel palette, string id)
    {
        palette.Open();
        return palette.Entries.Single(e => e.Id == id);
    }

    [Theory]
    [InlineData(false, "Rebuild views: done")]
    [InlineData(true, "SND-API-005: failed")]
    public async Task A_run_whose_palette_closed_while_it_sent_is_spoken_once_by_the_shell_region(bool refused, string outcome)
    {
        if (refused)
        {
            _server.Rebuild = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-005", "failed");
        }

        _server.RebuildGate = new TaskCompletionSource();
        var (palette, region, said) = Closing(bannerShowsControl: false);
        var rebuild = Find(palette, "RebuildViews");
        var control = rebuild.Control!;
        var controlSpoke = false;
        control.PropertyChanged += (_, e) => controlSpoke |= e.PropertyName == nameof(ControlViewModel.IsAnnouncing) && control.IsAnnouncing;

        var run = palette.Run(rebuild, Token);
        palette.Close();
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, said.Count(t => t.Contains(outcome, StringComparison.Ordinal)));
        Assert.True(region.IsLive);
        Assert.False(controlSpoke);
        Assert.False(control.IsMuted);   // the run is over: the control is back to speaking its own runs
    }

    [Fact]
    public async Task A_run_whose_palette_closed_is_handed_back_to_a_control_whose_view_is_on_show()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var (palette, _, said) = Closing(bannerShowsControl: true);
        var rebuild = Find(palette, "RebuildViews");

        var run = palette.Run(rebuild, Token);
        palette.Close();
        _server.RebuildGate.SetResult();
        await run;

        Assert.Empty(said);
        Assert.Equal((true, "Rebuild views: done"), (rebuild.Control!.IsAnnouncing, rebuild.Control.Announcement));
    }

    [Fact]
    public async Task The_close_first_policy_speaks_through_the_shell_region_too()
    {
        _server.RebuildGate = new TaskCompletionSource();
        var (palette, _, said) = Closing(bannerShowsControl: false);
        palette.RunPolicy = PaletteRunPolicy.CloseFirst;
        var rebuild = Find(palette, "RebuildViews");

        var run = palette.Run(rebuild, Token);
        _server.RebuildGate.SetResult();
        await run;

        Assert.Equal(1, said.Count(t => t.Contains("Rebuild views: done", StringComparison.Ordinal)));
        Assert.False(rebuild.Control!.IsAnnouncing);
    }

    // The region: the flag changes before the text, the text is never empty, and the same text twice is a change.
    [Fact]
    public void The_shell_region_says_the_same_text_twice_flag_then_text_and_never_empty()
    {
        var region = new CommandOutcomeRegion(_ => false);
        var steps = new List<string>();
        region.PropertyChanged += (_, e) => steps.Add(e.PropertyName + "=" + (e.PropertyName == nameof(CommandOutcomeRegion.Text) ? region.Text : region.IsLive));

        region.Say("Rebuild views: done");
        region.Say("Rebuild views: done");

        Assert.Equal(2, steps.Count(s => s == "Text=Rebuild views: done"));
        Assert.All(steps.Where(s => s.StartsWith("Text=", StringComparison.Ordinal)), s => Assert.NotEqual("Text=", s, StringComparer.Ordinal));
        Assert.True(region.IsLive);
    }

    // K5 (testing-3): a copy from the palette is spoken by the palette alone; the control stays dark while it copies and is
    // let go of when "Copied" clears.
    [Fact]
    public async Task A_palette_copy_is_spoken_by_the_palette_while_its_control_is_muted_until_copied_clears()
    {
        _palette.Open();
        _palette.Query = "set profile key";
        var entry = _palette.Selected!;
        var control = entry.Control!;
        var controlSpoke = false;
        control.PropertyChanged += (_, e) => controlSpoke |= e.PropertyName == nameof(ControlViewModel.IsAnnouncing) && control.IsAnnouncing;

        await _palette.Run(entry, Token);

        Assert.Equal((true, false), (control.IsMuted, controlSpoke));
        Assert.Contains("Copied", _palette.Announcement, StringComparison.Ordinal);
        _clock.Advance(ControlViewModel.CopiedFor);
        Assert.Equal((false, false), (control.IsMuted, controlSpoke));
    }

    // arch-0: a destructive command is confirmed in its own control, never from the palette: its entry is not a runnable stop,
    // arrowing skips it, and Enter on it neither asks nor sends.
    [Fact]
    public async Task A_destructive_entry_is_not_a_runnable_stop_and_enter_does_nothing_with_it()
    {
        var rows = _catalog.Rows.Select(r => string.Equals(r.Action, "RebuildViews", StringComparison.Ordinal) ? r with { Destructive = true } : r).ToList();
        var controls = new CatalogViewModel(_clipboard, _clock);
        controls.Show(_api, _server, _catalog with { Rows = rows });
        var palette = new CommandPaletteViewModel(controls, _pages.Add, _history.Add);
        palette.Open();
        var entry = palette.Entries.Single(e => e.Id == "RebuildViews");
        var stops = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            palette.Move(1);
            stops.Add(palette.Selected!.Id);
        }

        await palette.Run(entry, Token);

        Assert.Equal((false, false, 0, 0), (entry.IsEnabled, stops.Contains("RebuildViews"), _server.RebuildCalls, entry.Control!.IsConfirming ? 1 : 0));
    }
}
