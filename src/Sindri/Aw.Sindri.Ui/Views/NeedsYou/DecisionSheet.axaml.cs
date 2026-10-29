using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>
/// The decision sheet: a press on an option asks the view model to send Decide with it. The option buttons are rebuilt when
/// the options change (sending, a refusal), so focus goes back to the button of the option that was pressed: it is never lost.
/// </summary>
internal sealed partial class DecisionSheet : UserControl
{
    private string? _pressed;
    private bool _keyboard;

    public DecisionSheet()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DecisionSheetViewModel sheet)
            {
                sheet.PropertyChanged += (_, e) =>
                {
                    if (_pressed is not null && string.Equals(e.PropertyName, nameof(DecisionSheetViewModel.Options), StringComparison.Ordinal))
                    {
                        Dispatcher.UIThread.Post(RestoreFocus, DispatcherPriority.Loaded);
                    }
                };
            }
        };
    }

    private void OnCopyDecide(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DecisionSheetViewModel sheet)
        {
            _ = sheet.CopyDecide();
        }
    }

    private void OnOption(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DecisionSheetViewModel sheet && sender is Control { DataContext: DecisionOption option })
        {
            _pressed = option.Id;
            _keyboard = (sender as Control)?.Classes.Contains(":focus-visible") == true;
            _ = sheet.Choose(option.Id);
        }
    }

    // The options changed under the press (all disabled once it was accepted): focus goes back to the pressed option while it can
    // take it, else to the sheet's heading; never nowhere. The ring follows the way the press was made.
    private void RestoreFocus()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.FocusManager?.GetFocusedElement() is Control { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
        {
            return;
        }

        var button = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyEnabled && b.DataContext is DecisionOption o && string.Equals(o.Id, _pressed, StringComparison.Ordinal));
        _ = (button ?? (top as Control)?.FindControl<Control>("SheetTitle"))?.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }
}
#pragma warning restore MA0182
