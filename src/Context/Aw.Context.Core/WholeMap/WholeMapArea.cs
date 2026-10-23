namespace Aw.Context.Core.WholeMap;

/// <summary>One area: a top-level feature and everything the rules of <see cref="MapAreas"/> put under it, or the group of what no feature claims.</summary>
/// <param name="Key">The root feature's key, or <see cref="AreaIndex.UnassignedKey"/>.</param>
/// <param name="Label">The root feature's label as the caller reads it, or the unassigned group's name.</param>
/// <param name="Trust">Whether the label may be read as instructions.</param>
/// <param name="Members">Nodes whose area it is (not those that are only 'also in' it).</param>
/// <param name="ByKind">Members per kind, kinds with none left out, in kind order.</param>
/// <param name="Unassigned">True for the group of nodes no feature claims.</param>
public sealed record WholeMapArea(string Key, string Label, Results.Trust Trust, int Members, IReadOnlyDictionary<string, int> ByKind, bool Unassigned);
