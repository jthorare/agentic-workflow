using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The sheet's actions (<see cref="ItemSheetViewModel"/>, continued): the item's 'actions' as the catalog's controls.</summary>
internal sealed partial class ItemSheetViewModel
{
    /// <summary>
    /// Raised before controls leave <see cref="Actions"/> (they are still in it): the view moves focus off them first, so
    /// focus is never left on a control that is gone.
    /// </summary>
    internal event EventHandler<ControlsLeavingEventArgs>? ControlsLeaving;

    // The item's actions present in the catalog, in the API's order. A control lives as long as the item allows its
    // action, so its lifecycle (sending, the accepted outcome, an error) survives a refresh (§4.1); and a control in
    // flight or showing its outcome stays after the item stops allowing the action, until it settles (L8).
    private void ShowActions(IReadOnlyList<string> actions)
    {
        _allowed = actions;
        Reconcile(settled: false);
    }

    // settled: true when a control's own phase changed (it may go), false when the item's actions were re-read (a Failed one
    // that the item no longer allows goes with this re-read; Sending and Accepted ones stay until they settle).
    private void Reconcile(bool settled)
    {
        foreach (var gone in _controls.Where(c => !_allowed.Contains(c.Key, StringComparer.Ordinal) && !Lingers(c.Value, settled)).Select(c => c.Key).ToList())
        {
            _ = _controls.Remove(gone);
        }

        var shown = new List<ControlViewModel>();
        foreach (var action in _allowed.Concat(_controls.Keys.Except(_allowed, StringComparer.Ordinal)).ToList())
        {
            // Close item ends the item in one press, so the close actions ask first (Confirm and Keep, as Cancel job does), whatever the catalog says (ux-A-3).
            if (!_controls.TryGetValue(action, out var control) && catalog.On(action, ItemId, Sender(action), confirm: IsSheetAction(action)) is { } made)
            {
                control = _controls[action] = made;
                control.PropertyChanged += OnControlChanged;
            }

            if (control is not null)
            {
                shown.Add(control);
            }
        }

        PatchActions(shown);
    }

    private bool Lingers(ControlViewModel control, bool settled) =>
        control.Phase is CommandPhase.Sending or CommandPhase.Accepted || (settled && control.Phase == CommandPhase.Failed && !HasProblem);

    private void OnControlChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControlViewModel.Phase) && sender is ControlViewModel control
            && _controls.TryGetValue(control.Action, out var current) && ReferenceEquals(current, control))
        {
            Reconcile(settled: true);
        }
    }

    // The shown list, patched in place: the controls that stay keep their views (and the focus in them).
    private void PatchActions(List<ControlViewModel> shown)
    {
        var leaving = Actions.Except(shown).ToList();
        if (leaving.Count > 0)
        {
            ControlsLeaving?.Invoke(this, new ControlsLeavingEventArgs(leaving));
            foreach (var control in leaving)
            {
                _ = Actions.Remove(control);
            }
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (i < Actions.Count && ReferenceEquals(Actions[i], shown[i]))
            {
                continue;
            }

            var at = Actions.IndexOf(shown[i]);
            if (at >= 0)
            {
                Actions.Move(at, i);
            }
            else
            {
                Actions.Insert(i, shown[i]);
            }
        }

        OnChanged(nameof(HasActions));
    }

    /// <summary>True for an action only the item's sheet sends (it needs the version the sheet shows): the palette says "on the item's sheet" for it.</summary>
    /// <param name="action">The catalog action.</param>
    /// <returns>True when the sheet has a sender for it.</returns>
    internal static bool IsSheetAction(string action) => _closes.ContainsKey(action);

    // The actions the sheet sends, with the reason each closes the item for: the one set IsSheetAction and Sender read.
    private static readonly Dictionary<string, string> _closes = new(StringComparer.Ordinal)
    {
        ["CloseItem:done"] = "done",
        ["CloseItem:cancelled"] = "cancelled",
    };

    // What each action the sheet can send does. CloseItem's variants send the version shown at the click (§4.5); every
    // other action is drawn by the catalog (pending with its phase, or a copy block) and sends nothing from here.
    private Func<CancellationToken, Task<Result<CommandAcceptedView>>>? Sender(string action) =>
        _closes.TryGetValue(action, out var reason) ? ct => Close(action, reason, ct) : null;

    private async Task<Result<CommandAcceptedView>> Close(string action, string reason, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return SindriUiErrors.NotRunning.With("no API to send to");
        }

        Problem(string.Empty, string.Empty);
        Rest();
        var subject = _subject;
        var answer = await api.CloseItem(url, ItemId, new CloseItemRequest(reason, _version), cancellationToken).ConfigureAwait(true);
        if (answer.IsError && answer.Error.Code == Conflict && subject == _subject)
        {
            // §9.1: the item changed since the human looked. Re-read it. Whichever read wins settles the conflict (Show or
            // Failed): the pressed control shows the API's detail inline while the item still allows its action; when the
            // item no longer does (closed elsewhere), the sheet shows the detail in its own line instead, speaks it once, and
            // the control (muted) goes when it settles. An answer for an item the sheet has since left is no concern of the sheet.
            _conflict = (action, answer.Error);
            var ended = _conflictEnded ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = await Read(cancellationToken).ConfigureAwait(true);

            // A refresh may have taken the re-read over: Close does not return (and so the control does not speak the refusal)
            // until a read has settled the conflict, so the sheet and the control never both speak it.
            if (_conflict is not null)
            {
                await ended.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
        }

        return answer;
    }

    // The conflict a Close met, once the item is read again: the sheet speaks it when the item no longer allows the action.
    private void Settle(IReadOnlyList<string> allowed)
    {
        if (_conflict is not var (action, error))
        {
            return;
        }

        _conflict = null;
        try
        {
            if (allowed.Contains(action, StringComparer.Ordinal))
            {
                return;
            }

            if (_controls.TryGetValue(action, out var control))
            {
                control.IsMuted = true;
            }

            (_conflictAction, _conflictVersion) = (action, _version);
            Problem(error.Code, error.Message);
            Say($"{error.Code}: {error.Message}");
        }
        finally
        {
            // The waiter goes last: the press that waits for the settle returns only after the sheet has spoken.
            EndConflict();
        }
    }

    // Says a text through the polite region, as the queue's does: dark first, the resting text set, then live, then the text.
    private void Say(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.Sindri;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    private void Rest()
    {
        if (IsAnnouncing)
        {
            IsAnnouncing = false;
            Announcement = Labels.Sindri;
        }
    }
}
