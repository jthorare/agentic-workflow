using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A Reviews row asks the shell to open its PR's review page.</summary>
/// <param name="link">The review page's link.</param>
internal sealed class ReviewOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The review page's link.</summary>
    public DeepLink Link { get; } = link;
}
