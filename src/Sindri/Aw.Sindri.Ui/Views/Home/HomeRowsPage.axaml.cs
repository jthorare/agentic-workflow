using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Home;

namespace Aw.Sindri.Ui.Views.Home;

/// <summary>Home as rows (home spec §7.1, U1): a click on a tab selects its split, and the tab keeps keyboard focus when the tabs are drawn again.</summary>
#pragma warning disable MA0182 // why: ShellWindow.axaml places it, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class HomeRowsPage : UserControl
{
    public HomeRowsPage()
    {
        InitializeComponent();
    }

    // A selected split replaces the tabs (their counts and the selected one are new values), so the button that was clicked is gone: focus goes to
    // the new button of the same split once it is built.
    private void OnTab(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeRowsViewModel home || sender is not Control { DataContext: HomeTab tab } clicked)
        {
            return;
        }

        var hadFocus = clicked.IsKeyboardFocusWithin;
        home.Select(tab.Split);
        if (hadFocus)
        {
            Dispatcher.UIThread.Post(() => FocusTab(tab.Split), DispatcherPriority.Loaded);
        }
    }

    private void FocusTab(HomeSplit split) =>
        _ = HomeTabs.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(b => b.DataContext is HomeTab t && t.Split == split)?.Focus(NavigationMethod.Tab);
}
#pragma warning restore MA0182
