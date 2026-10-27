namespace Aw.Context.Core.Graph;

/// <summary>Node-kind rules: the scheme of each kind and the surface kinds a 'surface:' key may name (spec §6.1).</summary>
public static class NodeKinds
{
    /// <summary>The surface kinds: 'surface:&lt;kind&gt;/&lt;name&gt;'.</summary>
    public static IReadOnlySet<string> SurfaceKinds => SurfaceKindList.All;

    /// <summary>The kind a key scheme names.</summary>
    /// <param name="scheme">A scheme NodeKey accepted.</param>
    /// <returns>The kind.</returns>
    public static NodeKind FromScheme(string scheme) => scheme switch
    {
        "feature" => NodeKind.Feature,
        "surface" => NodeKind.Surface,
        "action" => NodeKind.Action,
        "thing" => NodeKind.Thing,
        "flow" => NodeKind.Flow,
        "decision" => NodeKind.Decision,
        "record" => NodeKind.Record,
        "concept" => NodeKind.Concept,
        _ => NodeKind.Code,
    };

    /// <summary>Whether a surface key names a known surface kind ('surface:command/split-vertical').</summary>
    /// <param name="key">The key.</param>
    /// <returns>True for a non-surface key or a known surface kind with a name.</returns>
    public static bool HasValidSurfaceKind(NodeKey key)
    {
        if (key.Kind != NodeKind.Surface)
        {
            return true;
        }

        var slash = key.Name.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 && slash < key.Name.Length - 1 && SurfaceKinds.Contains(key.Name[..slash]);
    }
}
