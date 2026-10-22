namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's selection, focus and keys (<see cref="RunPageViewModel"/>, continued): J and K walk the steps in reading order, Esc clears, F fits.</summary>
internal sealed partial class RunPageViewModel
{
    private bool _openPending;   // the run is shown but the stage had no size yet, so the opening view waits for it

    /// <summary>The canvas stage's size and the width of what covers its right side (the inspector), as the view reports them.</summary>
    internal (double Width, double Height, double RightInset) Viewport { get; private set; }

    /// <summary>The view the canvas opened on: the active stage at 100 %; null until the run is shown on a stage that has a size.</summary>
    internal RunFocus? Opening { get; private set; }

    /// <summary>The selected card or step: a card's id, or a step's key (the card's id, a slash and the step's id); null for none. The inspector (slice 4) shows it.</summary>
    public string? SelectedKey { get; private set => Set(ref field, value); }

    /// <summary>The selected step's or card's accessible name, for the live region; empty for none.</summary>
    public string SelectionName => SelectedStep?.AutomationName ?? Cards.FirstOrDefault(c => string.Equals(c.Id, SelectedKey, StringComparison.Ordinal))?.AutomationName ?? string.Empty;

    private RunStepViewModel? SelectedStep => Cards.SelectMany(c => c.Steps).FirstOrDefault(s => string.Equals(s.Key, SelectedKey, StringComparison.Ordinal));

    /// <summary>Tells the page how big the stage is; the first size after a read opens the canvas at the active stage.</summary>
    /// <param name="width">The stage's width.</param>
    /// <param name="height">The stage's height.</param>
    /// <param name="rightInset">The width of what covers the stage's right side (the inspector).</param>
    internal void SetViewport(double width, double height, double rightInset = 0)
    {
        Viewport = (width, height, rightInset);
        if (_openPending && IsShown)
        {
            OpenAtActive();
        }
    }

    /// <summary>The active stage at 100 %, centred in the visible part of the stage.</summary>
    /// <returns>The view; the identity view when there is no active stage yet.</returns>
    internal RunFocus FocusActive() =>
        ActiveNodeId is { } id && Layout.Rects.TryGetValue(id, out var rect)
            ? RunViewport.FocusActive(rect, Viewport.Width, Viewport.Height, Viewport.RightInset)
            : new RunFocus(1, 0, 0);

    /// <summary>The whole run on the stage (the F key and the Fit button).</summary>
    /// <returns>The view.</returns>
    internal RunFocus FitRun() => RunViewport.Fit(World, Viewport.Width, Viewport.Height);

    /// <summary>Sends the canvas to the active stage.</summary>
    internal void CentreOnActive() => Raise(FocusActive());

    /// <summary>Sends the canvas to the whole run.</summary>
    internal void FitToRun() => Raise(FitRun());

    private void OpenAtActive()
    {
        if (Viewport.Width <= 0 || Viewport.Height <= 0)
        {
            _openPending = true;
            return;
        }

        _openPending = false;
        Opening = FocusActive();
        Raise(Opening.Value);
    }

    /// <summary>Selects a card or a step (a click on a step row or a stage header).</summary>
    /// <param name="key">A card's id or a step's key.</param>
    internal void Select(string? key)
    {
        SelectedKey = key;
        Mark();
    }

    /// <summary>Clears the selection (a click on empty canvas, Esc).</summary>
    /// <returns>True when something was selected.</returns>
    internal bool ClearSelection()
    {
        if (SelectedKey is null)
        {
            return false;
        }

        Select(key: null);
        return true;
    }

    /// <summary>One single key on the page.</summary>
    /// <param name="key">J, K, F or Escape.</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key)
    {
        switch (key)
        {
            case "J":
                return Move(1);
            case "K":
                return Move(-1);
            case "F":
                FitToRun();
                return true;
            case "Escape":
                return ClearSelection();
            default:
                return false;
        }
    }

    // From no selection J goes to the first step and K to the last; at either end the selection stays. A selected card counts as its first step.
    private bool Move(int step)
    {
        var order = ReadingOrder;
        if (order.Count == 0)
        {
            return false;
        }

        var at = SelectedKey is null ? -1 : IndexOf(order, SelectedKey);
        var next = at < 0 ? (step > 0 ? 0 : order.Count - 1) : Math.Clamp(at + step, 0, order.Count - 1);
        Select(order[next]);
        return true;
    }

    private static int IndexOf(IReadOnlyList<string> order, string key)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], key, StringComparison.Ordinal) || order[i].StartsWith(key + "/", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // A re-read builds new cards: the selection is put back on them, or dropped when its step is gone.
    private void KeepSelection()
    {
        if (SelectedKey is { } key && IndexOf(ReadingOrder, key) < 0 && !Cards.Any(c => string.Equals(c.Id, key, StringComparison.Ordinal)))
        {
            SelectedKey = null;
        }

        Mark();
    }

    private void Mark()
    {
        foreach (var card in Cards)
        {
            card.IsSelected = SelectedKey is { } key && (string.Equals(card.Id, key, StringComparison.Ordinal) || key.StartsWith(card.Id + "/", StringComparison.Ordinal));
            foreach (var step in card.Steps)
            {
                step.IsSelected = string.Equals(step.Key, SelectedKey, StringComparison.Ordinal);
            }
        }

        OnChanged(nameof(SelectionName));
    }
}
