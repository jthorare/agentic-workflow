using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Views.Queue;

namespace Aw.Sindri.Ui.Views.Shell;

internal sealed partial class ShellWindow
{
    // The frame of a row on the page shown. The Work table builds only the rows in view (#2776), so a row far below is built first by scrolling to it.
    private Control? BuiltRowFrame(string itemId)
    {
        if (RowFrame(itemId) is { } frame)
        {
            return frame;
        }

        if (this.Get<Control>("Page").GetVisualDescendants().OfType<QueuePage>().FirstOrDefault() is not { } page || !page.ScrollToRow(itemId))
        {
            return null;
        }

        UpdateLayout();
        return RowFrame(itemId);
    }
}
