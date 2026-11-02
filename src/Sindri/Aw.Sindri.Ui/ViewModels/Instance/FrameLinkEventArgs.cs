using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame asks to open a place that is not another instance (a decision, a service, a recipe).</summary>
/// <param name="link">Where.</param>
internal sealed class FrameLinkEventArgs(DeepLink link) : EventArgs
{
    /// <summary>Where.</summary>
    public DeepLink Link { get; } = link;
}
