using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Which served nodes are pages; the same rule as the server's PageKinds.IsPage (Aw.Context.Core.Graph, pages spec §3.2), judged from a node read.</summary>
internal static class NodePageKinds
{
    private const string Prefix = "surface:";
    private static readonly string[] _componentExtensions = [".tsx", ".jsx"];

    /// <summary>Whether a node read is a page.</summary>
    /// <param name="view">The node read.</param>
    /// <returns>True for a 'screen' or 'dialog' surface, and for a 'route' implemented from a '.tsx' or '.jsx' file; never a 'pane', 'api' or any other kind.</returns>
    internal static bool IsPage(NodeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var key = view.Node.Key;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var name = key[Prefix.Length..];
        var slash = name.IndexOf('/', StringComparison.Ordinal);
        return (slash < 0 ? name : name[..slash]) switch
        {
            "screen" or "dialog" => true,
            "route" => view.In.Any(e => string.Equals(e.Type, "implements", StringComparison.Ordinal) && _componentExtensions.Any(x => e.From.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
            _ => false,
        };
    }
}
