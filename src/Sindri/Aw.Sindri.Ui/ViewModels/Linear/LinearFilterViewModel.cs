#pragma warning disable CA1822, MA0041, MA0182, CS0067 // why: the red stubs read nothing yet

using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One drop-down of the filter bar.</summary>
internal sealed class LinearFilterViewModel : ObservableObject
{
    internal LinearFilterViewModel(string key)
    {
        throw new NotSupportedException();
    }

    /// <summary>team, project, cycle, state, assignee, priority or label.</summary>
    public string Key => throw new NotSupportedException();

    /// <summary>The drop-down's accessible name.</summary>
    public string Label => throw new NotSupportedException();

    /// <summary>The choices; the first leaves the filter off.</summary>
    public ObservableCollection<LinearFilterChoice> Choices => throw new NotSupportedException();

    /// <summary>The choice picked.</summary>
    public LinearFilterChoice Selected { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>The picked choice's value; null when the filter is off.</summary>
    public string? Value => throw new NotSupportedException();

    /// <summary>Picks the choice with this value.</summary>
    /// <param name="value">The value; null turns the filter off.</param>
    internal void Pick(string? value) => throw new NotSupportedException();

    /// <summary>Raised when the pick changed.</summary>
    internal event EventHandler? Picked;

    /// <summary>Adds a choice unless one with the value exists.</summary>
    /// <param name="value">The value.</param>
    /// <param name="text">The words.</param>
    internal void Offer(string value, string text) => throw new NotSupportedException();
}
