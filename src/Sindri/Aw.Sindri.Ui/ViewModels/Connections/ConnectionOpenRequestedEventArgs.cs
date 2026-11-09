using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>A row asked to open its connection's sheet.</summary>
/// <param name="link">The connection's link.</param>
internal sealed class ConnectionOpenRequestedEventArgs(DeepLink link) : EventArgs
{
    /// <summary>The connection's link.</summary>
    public DeepLink Link { get; } = link;
}
