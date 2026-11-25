using Avalonia;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

// Every size the bar remembers after a measure (Remember) must come from that same measure (#2710).
internal sealed partial class TopBar
{
    // Re-fits the bar when the app's resources change. A text-scale change grows the tabs, but the bar's parent is
    // clamped to the window width, so its desired size stays the same and the change never reaches the bar's measure:
    // the bar would keep the step it chose at the old scale. The handler is the bar's own, so it goes with the bar.
    protected override void OnInitialized()
    {
        base.OnInitialized();
        ResourcesChanged += (_, _) => InvalidateMeasure();
    }

    // Measures the bar's parts afresh. A changed part invalidates only itself: the problem text's new cap, or a step's
    // class or visibility change. Its parents' measures stay valid, so the bar's measure skips them and never reaches
    // the changed part. Remember would then read the pill's old width next to the problem text's new one, the pill's
    // rest would swing between passes and the bar would never settle. So every part under the bar is measured again.
    private Size MeasureParts(Size availableSize)
    {
        foreach (var part in this.GetVisualDescendants())
        {
            if (part is Layoutable layoutable)
            {
                layoutable.InvalidateMeasure();
            }
        }

        return base.MeasureOverride(availableSize);
    }
}
