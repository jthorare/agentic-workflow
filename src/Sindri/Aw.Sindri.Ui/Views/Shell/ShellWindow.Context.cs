using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on the Context page (<see cref="ShellWindow"/>, continued; screens spec §4.4): opening the page starts focus in the anchor field.</summary>
internal sealed partial class ShellWindow
{
    // Queued after the page switch's own focus move (the page region, so the new page is spoken), so the anchor box ends up with it.
    private void FocusContextAnchor()
    {
        if (Shell is { CurrentPage: PageId.Context })
        {
            Dispatcher.UIThread.Post(() => _ = this.Get<Control>("ContextPage").FindControl<TextBox>("AnchorBox")?.Focus(NavigationMethod.Unspecified), DispatcherPriority.Loaded);   // Unspecified: the box takes focus without the focus-visible ring on every visit; the box is in the page's own name scope
        }
    }
}
