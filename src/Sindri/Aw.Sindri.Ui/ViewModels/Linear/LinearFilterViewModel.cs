using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One drop-down of the filter bar: its choices only grow (what the rows and the lookups offer), and a pick tells the view model to read again.</summary>
internal sealed class LinearFilterViewModel : ObservableObject
{
    private readonly Dictionary<string, LinearFilterChoice> _byValue = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _rank = new(StringComparer.Ordinal);
    private bool _quiet;

    internal LinearFilterViewModel(string key)
    {
        Key = key;
        Label = Labels.LinearFilterLabel(key);
        Selected = Off = new LinearFilterChoice(Value: null, Labels.LinearFilterAny(key));
        Choices = [Off];
    }

    /// <summary>team, project, cycle, state, assignee, priority or label.</summary>
    public string Key { get; }

    /// <summary>The drop-down's accessible name.</summary>
    public string Label { get; }

    /// <summary>The choices; the first leaves the filter off.</summary>
    public ObservableCollection<LinearFilterChoice> Choices { get; }

    /// <summary>The choice picked.</summary>
    public LinearFilterChoice Selected
    {
        get;
        set
        {
            if (value is null || ReferenceEquals(field, value))
            {
                return;   // the drop-down clears its selection while the choices change under it
            }

            field = value;
            OnChanged(nameof(Selected));
            OnChanged(nameof(Value));
            if (!_quiet)
            {
                Picked?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The picked choice's value; null when the filter is off.</summary>
    public string? Value => Selected.Value;

    /// <summary>Raised when the pick changed.</summary>
    internal event EventHandler? Picked;

    /// <summary>Picks the choice with this value; an unknown value picks nothing.</summary>
    /// <param name="value">The value; null turns the filter off.</param>
    internal void Pick(string? value)
    {
        if (value is null)
        {
            Selected = Off;
        }
        else if (_byValue.TryGetValue(value, out var choice))
        {
            Selected = choice;
        }
    }

    /// <summary>Turns the filter off without telling anyone (Clear filters reads once for all of them).</summary>
    internal void Reset()
    {
        _quiet = true;
        Selected = Off;
        _quiet = false;
    }

    /// <summary>Adds a choice unless one with the value exists; choices with a rank come first, in rank order, then the rest by name (A to Z).</summary>
    /// <param name="value">The value.</param>
    /// <param name="text">The words.</param>
    /// <param name="rank">Where the source puts it (a state's board position, a priority's order); null when it has none.</param>
    internal void Offer(string value, string text, int? rank = null)
    {
        var known = _byValue.ContainsKey(value);
        if (!known)
        {
            _byValue[value] = new LinearFilterChoice(value, text);
        }

        if (rank is { } r && !_rank.ContainsKey(value))
        {
            _rank[value] = r;
        }
        else if (known)
        {
            return;
        }

        Patch.To(Choices, [Off, .. _byValue.Values.OrderBy(c => _rank.GetValueOrDefault(c.Value ?? string.Empty, int.MaxValue)).ThenBy(c => c.Text, StringComparer.OrdinalIgnoreCase)]);
    }

    private LinearFilterChoice Off { get; }
}
