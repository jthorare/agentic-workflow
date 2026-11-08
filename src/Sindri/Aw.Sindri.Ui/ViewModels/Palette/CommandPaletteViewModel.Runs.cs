using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The command palette (<see cref="CommandPaletteViewModel"/>, continued): the runs it started and how it announces them.</summary>
internal sealed partial class CommandPaletteViewModel
{
    // The entries the palette ran whose control it mutes until it has announced the outcome.
    private readonly Dictionary<PaletteEntry, Ran> _ran = [];

    // One run the palette announces: a copy or a send, and the entry's name as last said, so the same status is said once
    // (the outcome arrives through several changes) and a new run says its outcome even when it equals the last run's.
    private sealed class Ran(bool copy)
    {
        public bool Copy { get; } = copy;

        public string? Said { get; set; }

        // True once the palette closed on the run: the shell's region speaks the rest of it.
        public bool Closed { get; set; }
    }

    // The palette speaks the lifecycle of the entries it runs through its own live region, so it mutes those entries' controls
    // for that run and no others (a run that did not start here is spoken by its control, whatever is open): the run is
    // announced once. The mute ends when the run's outcome is announced. When the palette closes first, a run still sending is
    // spoken on by the shell's command-outcome region, unless its control's own view is on show (then the control speaks it).
    private void Track(PaletteEntry entry, ControlViewModel control, bool copy)
    {
        Release(entry);
        _ran[entry] = new Ran(copy);
        control.IsMuted = true;
        entry.PropertyChanged += OnRanChanged;
    }

    private void Release(PaletteEntry entry)
    {
        if (_ran.Remove(entry))
        {
            // The shell region stays live after the outcome: going dark in the same tick could stop a screen reader speaking it; the next Say rests it first.
            entry.PropertyChanged -= OnRanChanged;
            entry.Control?.IsMuted = false;
        }
    }

    private void ReleaseAll() => _ran.Keys.ToList().ForEach(Release);

    // The palette closed. A copy is over with its "Copied". A send in flight stays muted and is spoken by the shell's region
    // (it always is in the tree); with no region, or when the control's view is on show, its control speaks it.
    private void HandBack()
    {
        foreach (var (entry, ran) in _ran.ToList())
        {
            if (Outcomes is { } region && !ran.Copy && entry.Control is { } control && !region.ViewSpeaks(control))
            {
                ran.Closed = true;
            }
            else
            {
                Release(entry);
            }
        }
    }

    private async Task Copy(PaletteEntry entry, ControlViewModel control, CancellationToken cancellationToken)
    {
        Track(entry, control, copy: true);
        await control.Copy(cancellationToken).ConfigureAwait(true);
        if (!IsCopied(control))
        {
            Release(entry);   // nothing was copied: nothing to announce, nothing to wait for
        }
        else if (_ran.TryGetValue(entry, out var ran))
        {
            Say(entry, ran);   // a second Copy inside the 1.5 s changes nothing the entry shows; this says "Copied" again
        }
    }

    private async Task Send(PaletteEntry entry, ControlViewModel control, CancellationToken cancellationToken)
    {
        Track(entry, control, copy: false);
        if (RunPolicy == PaletteRunPolicy.CloseFirst)
        {
            Close();
        }

        try
        {
            await control.Press(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            if (!control.IsSending)
            {
                Release(entry);   // normally released at the outcome already; a cancelled send leaves the control Idle (ControlViewModel.Run), which this covers
            }
        }
    }

    private static bool IsCopied(ControlViewModel control) => string.Equals(control.CopyLabel, Labels.Copied, StringComparison.Ordinal);

    // A run of the palette's own changed. Its outcome, "Sending…" (as the control would say it, after 300 ms) and "Copied" are
    // announced: as the selected entry with its position, or by its name alone when the selection or the query moved on. What
    // is not announced: the first 300 ms of a send (the region would say the entry again) and the settling back to ready.
    private void OnRanChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PaletteEntry.Name) || sender is not PaletteEntry { Control: { } control } entry || !_ran.TryGetValue(entry, out var ran))
        {
            return;
        }

        if (Speaks(control))
        {
            Say(entry, ran);
        }

        if (ran.Copy ? !IsCopied(control) : control.IsAccepted || control.IsFailed)
        {
            Release(entry);
        }
    }

    private static bool Speaks(ControlViewModel control) => control.IsAnnouncingSending || control.IsAccepted || control.IsFailed || IsCopied(control);

    // Says the entry's status once: skipped when its name is what this run said last. A text the region already holds (the
    // same refusal on a retry) is rested on a neutral text with the region dark first, as the controls do, so it is a change.
    private void Say(PaletteEntry entry, Ran ran)
    {
        if (string.Equals(ran.Said, entry.Name, StringComparison.Ordinal))
        {
            return;
        }

        ran.Said = entry.Name;
        if (ran.Closed)
        {
            Outcomes?.Say(entry.Name);
            return;
        }

        var text = entry == Selected ? Describe() : entry.Name;
        if (string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;
            Announcement = Labels.Palette;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    // The entries the palette runs itself, with no send: each closes it. They have no page, so the page case can't take them.
    private bool RunsHere(PaletteEntry entry)
    {
        switch (entry)
        {
            case { Id: PaletteEntry.TodoId }:
                _openTodo?.Invoke();
                Close();
                return true;
            case { Control.Action: "StopProcess" }:
                Close();
                _goTo(PageId.System);
                return true;
            case { Control.Action: "CreateItem" }:
                // The form's or dialog's own control sends it; it opens first, so the window hands the palette's opener on to it.
                CreateItem?.Invoke();
                Close();
                return true;
            case { Id: PaletteEntry.NewTodoId }:
                NewTodo?.Invoke();
                Close();
                return true;
            default:
                return false;
        }
    }
}
