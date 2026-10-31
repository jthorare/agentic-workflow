namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One feature on the Pin sheet.</summary>
internal sealed class PinFeatureRow(string key, string label, bool ticked) : ObservableObject
{
    /// <summary>The feature's exact key.</summary>
    public string Key { get; } = key;

    /// <summary>Its label.</summary>
    public string Label { get; } = label;

    /// <summary>The picker's button on a found feature.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string AddText => Labels.PinSheetAddFeature;
#pragma warning restore CA1822, MA0041

    /// <summary>Whether it goes into the pin's features.</summary>
    public bool Ticked
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnChanged(nameof(Ticked));
                TickChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    } = ticked;

    /// <summary>The tick changed: the sheet counts it as an edit.</summary>
    internal event EventHandler? TickChanged;
}
