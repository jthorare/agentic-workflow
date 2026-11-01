using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context;

/// <summary>The sheets over the Context page (<see cref="ContextPage"/>, continued): the page sheet and the Add several pages sheet remember the button that opened them and give focus back when they go.</summary>
internal sealed partial class ContextPage
{
    private static bool IsSheetProperty(string? name) =>
        string.Equals(name, nameof(ContextPageViewModel.ShowPageSheet), StringComparison.Ordinal) || string.Equals(name, nameof(ContextPageViewModel.ShowAddSeveralPages), StringComparison.Ordinal);

    // A sheet opening remembers the button that asked; its closing gives focus back.
    private void SheetFocus(bool shown)
    {
        if (shown)
        {
            _opener = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        }
        else
        {
            ReturnFocus();
        }
    }
}
