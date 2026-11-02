using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when a project's sheet closes (<see cref="ShellWindow"/>, continued): Esc returns it to the card that opened the sheet.</summary>
internal sealed partial class ShellWindow
{
    // The first visible, focusable control of the page whose data is the card that opened the sheet (the card's own frame).
    private Control? OpenerProjectFrame() => Shell?.OpenerAdd == true ? OpenerAddButton() : Shell?.OpenerProject is { } id
        ? Usable(this.Get<Border>("Page").GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c is { Focusable: true, IsEffectivelyVisible: true, DataContext: ProjectCardViewModel card } && string.Equals(card.Id, id, StringComparison.Ordinal)))
        : null;

    // The Add project button that opened the sheet: the header's, or the empty block's.
    private Control? OpenerAddButton()
    {
        var buttons = this.Get<Border>("Page").GetVisualDescendants().OfType<Button>().ToList();
        return Usable(buttons.FirstOrDefault(b => string.Equals(b.Name, "ProjectsAdd", StringComparison.Ordinal)))
            ?? Usable(buttons.FirstOrDefault(b => string.Equals(b.Name, "ProjectsAddEmpty", StringComparison.Ordinal)));
    }
}
