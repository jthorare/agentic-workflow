using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Instance;

#pragma warning disable MA0182 // created by the Why flyout in the Model picks row template, which the analyzer does not read
/// <summary>
/// The Why flyout's content. An open flyout's popup is not under the frame, so Esc is caught here, on the way down at the top level that
/// holds the popup: it closes this flyout only, puts focus back on the sentence that opened it and spends the key, so the sheet stays and
/// the next Esc closes it (model-pick spec section 6.1, Layering).
/// </summary>
internal sealed partial class WhyPanel : UserControl
{
    private TopLevel? _root;

    public WhyPanel()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _root = TopLevel.GetTopLevel(this);
        _root?.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _root?.RemoveHandler(KeyDownEvent, OnPreviewKeyDown);
        _root = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || this.FindAncestorOfType<FlyoutPresenter>()?.Parent is not Popup { PlacementTarget: Button opener })
        {
            return;
        }

        opener.Flyout?.Hide();
        _ = opener.Focus(NavigationMethod.Tab);
        e.Handled = true;
    }
}
