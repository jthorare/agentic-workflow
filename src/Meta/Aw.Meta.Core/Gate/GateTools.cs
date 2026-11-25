namespace Aw.Meta.Core.Gate;

/// <summary>Where a gate tree keeps the tools its install step put there.</summary>
public static class GateTools
{
    /// <summary>The directories that go in front of PATH for a command in this gate tree: its node_modules/.bin, as `npm run` does.</summary>
    /// <param name="tree">The gate tree.</param>
    /// <returns>The directories, first one first.</returns>
    public static IReadOnlyList<string> PathPrefix(string tree) => [tree.TrimEnd('/') + "/node_modules/.bin"];
}
