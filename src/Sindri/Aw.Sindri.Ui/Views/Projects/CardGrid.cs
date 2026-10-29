using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml sets it, which the analyzer cannot see
/// <summary>Keyboard use of a Projects card grid: one tab stop, arrow keys move by card (projects page spec §3.3).</summary>
internal static class CardGrid
{
    /// <summary>True on a control whose cards form one grid.</summary>
    internal static readonly AttachedProperty<bool> IsGridProperty = AvaloniaProperty.RegisterAttached<Control, bool>("IsGrid", typeof(CardGrid));

    /// <summary>Reads <see cref="IsGridProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <returns>True when it is a card grid.</returns>
    internal static bool GetIsGrid(Control element) => element.GetValue(IsGridProperty);

    /// <summary>Sets <see cref="IsGridProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <param name="value">True to make it a card grid.</param>
    internal static void SetIsGrid(Control element, bool value) => element.SetValue(IsGridProperty, value);
}
#pragma warning restore MA0182
