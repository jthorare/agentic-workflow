using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A card asks the shell to open its sheet.</summary>
/// <param name="link">The project's link.</param>
internal sealed class ProjectOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The project's link.</summary>
    public DeepLink Link { get; } = link;
}
