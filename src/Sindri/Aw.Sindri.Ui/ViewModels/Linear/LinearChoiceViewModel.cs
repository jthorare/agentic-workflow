using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One entry of a picker or one tick of the labels: the value it stands for (an id, a priority number, or null for the clear entry) and its name.</summary>
/// <param name="value">The value sent for it.</param>
/// <param name="name">Its name in words.</param>
internal sealed class LinearChoiceViewModel(JsonNode? value, string name) : ObservableObject
{
    private int _quiet;

    /// <summary>The value sent for this entry; null clears.</summary>
    public JsonNode? Value { get; } = value;

    /// <summary>The entry's name.</summary>
    public string Name { get; } = name;

    /// <summary>For a label: true while it is ticked.</summary>
    public bool IsTicked
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnChanged(nameof(IsTicked));
                if (Volatile.Read(ref _quiet) == 0)
                {
                    Ticked?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    /// <summary>The tick changed by the owner or by a reset.</summary>
    internal event EventHandler? Ticked;

    /// <summary>Sets the tick without raising <see cref="Ticked"/> (the field is resetting itself, not the owner ticking).</summary>
    /// <param name="ticked">The tick.</param>
    internal void Reset(bool ticked)
    {
        if (IsTicked != ticked)
        {
            _ = Interlocked.Exchange(ref _quiet, 1);
            IsTicked = ticked;
            _ = Interlocked.Exchange(ref _quiet, 0);
        }
    }
}
