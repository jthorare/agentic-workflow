#pragma warning disable CA1822, MA0041, MA0182 // red commit: stub, replaced by the green commit
namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>One service tile on the Connections page: a service with a sign-in path, its Connect action, and, once connected, the account, last use and menu.</summary>
internal sealed class ServiceTileViewModel
{
    /// <summary>The service id ('github').</summary>
    public string Id { get; } = string.Empty;

    /// <summary>The service in words ('GitHub').</summary>
    public string Title { get; } = string.Empty;
}
