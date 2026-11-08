using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's two focus regions (<see cref="HomePage"/>, continued; todo spec D6): the Todo panel and the Needs you list tell the view models which one has focus.</summary>
internal sealed partial class HomePage
{
    // Focus arriving in a region marks it, focus leaving clears it (the next region's arrival marks that one); a todo row that takes focus is the panel's selection.
    private void Region(RoutedEventArgs e, bool on)
    {
        if (DataContext is not ShellViewModel shell || e.Source is not Visual source)
        {
            return;
        }

        var path = source.GetSelfAndVisualAncestors().OfType<Control>().ToList();
        if (on && path.Find(c => c.Classes.Contains("todo-row"))?.DataContext is TodoRowViewModel row)
        {
            shell.Todo.SelectedId = row.Id;
        }

        if (InRegion(path, "TodoPanelFrame"))
        {
            shell.Todo.HasFocus = on;
        }
        else if (InRegion(path, "NeedsYouPanel"))
        {
            shell.IsNeedsYouFocused = on;
        }
    }

    private static bool InRegion(List<Control> path, string name) => path.Exists(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}
