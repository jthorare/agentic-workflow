using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The Building overlay's View build job was pressed: the link that opens the Jobs page on that job's sheet.</summary>
/// <param name="link">The job's link.</param>
internal sealed class ContextJobRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>Where View build job goes.</summary>
    public DeepLink Link { get; } = link;
}
