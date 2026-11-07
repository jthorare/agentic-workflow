using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when the library errors sheet closes (<see cref="ShellWindow"/>, continued): Esc and Close return it to the Reviews header's library button.</summary>
internal sealed partial class ShellWindow
{
    private Control? OpenerLibraryButton() => Shell?.OpenerLibrary == true
        ? Usable(this.Get<Border>("Page").GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "ReviewsLibrary", StringComparison.Ordinal)))
        : null;
}
