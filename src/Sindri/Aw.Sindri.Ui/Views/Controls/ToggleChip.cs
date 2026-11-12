#pragma warning disable MA0182 // until a page places the control

using Avalonia.Controls.Primitives;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The toggle chip (#3234): one small bordered word that is either a toggle (the Advanced mode switch) or, not clickable, a status
/// chip (the mode chip, the findings chip, a run row's Test chip). Stub until the part is written.
/// </summary>
internal sealed class ToggleChip : ToggleButton
{
    /// <summary>Gets or sets the word the chip shows and the screen reader reads.</summary>
    public string? Word { get; set; }

    /// <summary>Gets or sets the tone: ok, running, waiting, attention, error, neutral or draft (the status-chip set).</summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>Gets or sets a value indicating whether the chip can be clicked and focused.</summary>
    public bool Interactive { get; set; } = true;
}
