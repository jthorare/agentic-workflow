using Avalonia.Input;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Toast;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Toast;

/// <summary>The shell's one toast slot (todo spec §3.3, D11): replace, a pick that is never replaced, the countdown, the pause, Ctrl+Z, Esc.</summary>
[Lane(TestLane.Unit)]
public sealed class ToastHostTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly List<string> _undone = [];

    private ToastHostViewModel Host(bool macOS = false) => new(_clock, macOS);

    private ToastMessage Plain(string text) => new(text, () =>
    {
        _undone.Add(text);
        return Task.CompletedTask;
    });

    private ToastMessage Pick(string text, int seconds = 10) => new(text, () =>
    {
        _undone.Add(text);
        return Task.CompletedTask;
    }, ToastKind.Pick, seconds);

    private void Wait(int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task A_newer_toast_replaces_an_older_one_and_only_the_newest_Undo_works()
    {
        var host = Host();
        host.Show(Plain("Done."));
        host.Show(Plain("Removed."));

        Assert.Equal("Removed.", host.Text);
        Assert.True(await host.Undo());
        Assert.Equal(["Removed."], _undone);
        Assert.False(host.IsShown);
        Assert.False(await host.Undo());   // the older one's Undo is gone with its toast
    }

    [Fact]
    public void A_plain_toast_ends_after_10_s()
    {
        var host = Host();
        host.Show(Plain("Done."));

        Wait(9);
        Assert.True(host.IsShown);
        Wait(1);
        Assert.False(host.IsShown);
    }

    [Fact]
    public void A_pick_toast_is_never_replaced_and_a_newer_toast_waits_until_its_window_closes()
    {
        var host = Host();
        host.Show(Pick("Kept Obsidian's version."));
        Wait(3);
        host.Show(Plain("Done."));

        Assert.Equal("Kept Obsidian's version.", host.Text);
        Assert.Equal(1, host.PendingCount);

        Wait(6);
        Assert.Equal("Kept Obsidian's version.", host.Text);   // 9 s in
        Wait(1);
        Assert.Equal("Done.", host.Text);                     // the window closed: the waiting toast shows
        Assert.False(host.IsPickOpen);
        Assert.Equal(0, host.PendingCount);
    }

    [Fact]
    public void Of_several_toasts_waiting_the_newest_plain_one_wins_and_a_pick_is_kept()
    {
        var host = Host();
        host.Show(Pick("first pick"));
        host.Show(Plain("a"));
        host.Show(Plain("b"));
        host.Show(Pick("second pick"));
        host.Show(Plain("c"));

        Wait(10);
        Assert.Equal("second pick", host.Text);   // the waiting plain toasts before it were replaced by it
        Wait(10);
        Assert.Equal("c", host.Text);
    }

    [Fact]
    public void A_pick_toast_counts_down_each_second()
    {
        var host = Host();
        host.Show(Pick("Kept Obsidian's version."));
        Assert.Equal("Undo (10 s)", host.UndoLabel);

        Wait(1);
        Assert.Equal("Undo (9 s)", host.UndoLabel);
        Wait(8);
        Assert.Equal("Undo (1 s)", host.UndoLabel);
        Wait(1);
        Assert.False(host.IsShown);
    }

    [Fact]
    public void A_pick_toast_never_pauses()
    {
        var host = Host();
        host.Show(Pick("Kept Obsidian's version."));
        host.SetPointerOver(over: true);
        host.SetFocused(focused: true);

        Wait(4);
        Assert.False(host.IsPaused);
        Assert.Equal("Undo (6 s)", host.UndoLabel);
    }

    [Fact]
    public void A_plain_toast_with_Undo_pauses_while_the_pointer_or_focus_is_on_it()
    {
        var host = Host();
        host.Show(Plain("Done."));
        Wait(4);

        host.SetPointerOver(over: true);
        Wait(30);
        Assert.True(host.IsShown);
        Assert.Equal(6, host.Remaining);

        host.SetPointerOver(over: false);
        host.SetFocused(focused: true);
        Wait(30);
        Assert.Equal(6, host.Remaining);

        host.SetFocused(focused: false);
        Wait(6);
        Assert.False(host.IsShown);
    }

    [Fact]
    public void A_plain_toast_without_Undo_does_not_pause()
    {
        var host = Host();
        host.Show(new ToastMessage("Sindri isn't running. Start it, then add the todo."));
        host.SetPointerOver(over: true);

        Wait(10);
        Assert.False(host.IsShown);
    }

    [Fact]
    public async Task Ctrl_Z_runs_the_visible_toasts_Undo_outside_a_text_field()
    {
        var host = Host();
        host.Show(Plain("Done."));

        Assert.True(host.PressUndoKey(inTextField: false));
        await Task.Yield();
        Assert.Equal(["Done."], _undone);
        Assert.False(host.IsShown);
        Assert.False(host.PressUndoKey(inTextField: false));   // nothing shows now
    }

    [Fact]
    public void Ctrl_Z_in_a_text_field_does_nothing_of_ours()
    {
        var host = Host();
        host.Show(Plain("Done."));

        Assert.False(host.PressUndoKey(inTextField: true));
        Assert.Empty(_undone);
        Assert.True(host.IsShown);
    }

    [Fact]
    public void The_Undo_key_is_Control_Z_and_on_macOS_Command_Z()
    {
        Assert.True(ShellWindow.IsToastUndoKey(Key.Z, KeyModifiers.Control, macOS: false));
        Assert.False(ShellWindow.IsToastUndoKey(Key.Z, KeyModifiers.Meta, macOS: false));
        Assert.True(ShellWindow.IsToastUndoKey(Key.Z, KeyModifiers.Meta, macOS: true));
        Assert.False(ShellWindow.IsToastUndoKey(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, macOS: false));
        Assert.False(ShellWindow.IsToastUndoKey(Key.Y, KeyModifiers.Control, macOS: false));
    }

    [Fact]
    public void Esc_dismisses_a_plain_toast_only_while_focus_is_on_it()
    {
        var host = Host();
        host.Show(Plain("Done."));

        Assert.False(host.PressEscape(focusOnToast: false));
        Assert.True(host.IsShown);

        Assert.True(host.PressEscape(focusOnToast: true));
        Assert.False(host.IsShown);
    }

    [Fact]
    public void A_dismissed_pick_toast_hides_but_its_window_still_runs_to_the_end()
    {
        var host = Host();
        host.Show(Pick("Kept Obsidian's version."));
        Wait(2);

        Assert.True(host.PressEscape(focusOnToast: true));
        Assert.False(host.IsShown);
        Assert.True(host.IsPickOpen);
        Assert.False(host.PressUndoKey(inTextField: false));   // hidden: no Undo from the keyboard

        host.Show(Plain("Done."));   // still waits behind the hidden window
        Wait(7);
        Assert.False(host.IsShown);
        Assert.True(host.IsPickOpen);
        Wait(1);
        Assert.Equal("Done.", host.Text);
        Assert.False(host.IsPickOpen);
    }

    [Fact]
    public async Task Undoing_a_pick_closes_its_window_and_shows_the_toast_that_waited()
    {
        var host = Host();
        host.Show(Pick("Kept Obsidian's version."));
        host.Show(Plain("Done."));

        Assert.True(await host.Undo());
        Assert.Equal(["Kept Obsidian's version."], _undone);
        Assert.Equal("Done.", host.Text);
    }

    [Fact]
    public void The_polite_region_says_the_key_once_and_a_pick_adds_its_window()
    {
        var host = Host();
        var spoken = new List<string>();
        host.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToastHostViewModel.Announcement) && host.Announcement.Length > 0)
            {
                spoken.Add(host.Announcement);
            }
        };

        host.Show(Plain("Done."));
        Wait(3);   // the ticks say nothing
        Assert.Equal(["Done. Undo with Control Z"], spoken);
        Assert.True(host.IsAnnouncing);

        host.Show(Plain("Done."));   // the same words again are spoken again
        Assert.Equal(2, spoken.Count);

        host.Show(Pick("Kept Obsidian's version."));
        Assert.Equal("Kept Obsidian's version. Undo with Control Z within 10 seconds", spoken[^1]);
    }

    [Fact]
    public void On_macOS_the_words_name_Command_Z()
    {
        var host = Host(macOS: true);
        host.Show(Plain("Done."));

        Assert.Equal("Done. Undo with Command Z", host.Announcement);
        Assert.Equal("Undo (⌘Z)", host.UndoLabel);
    }
}
