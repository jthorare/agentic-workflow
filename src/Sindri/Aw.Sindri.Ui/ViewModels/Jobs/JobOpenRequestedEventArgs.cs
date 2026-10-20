using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>A job row asks the shell to open its job's sheet.</summary>
/// <param name="link">The job's link.</param>
internal sealed class JobOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The job's link.</summary>
    public DeepLink Link { get; } = link;
}
