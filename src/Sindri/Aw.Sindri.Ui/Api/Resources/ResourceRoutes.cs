namespace Aw.Sindri.Ui.Api.Resources;

/// <summary>The paths of the resource calls.</summary>
internal static class ResourceRoutes
{
    /// <summary>The list.</summary>
    internal const string Root = "/sindri/resources";

    /// <summary>The purge.</summary>
    internal const string Purge = Root + "/purge";

    internal static string Listed(string? owner) => owner is { Length: > 0 } ? $"{Root}?owner={Uri.EscapeDataString(owner)}" : Root;
}
