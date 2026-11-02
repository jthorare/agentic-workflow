using Aw.Context.Core.Surfaces;

namespace Aw.Context.Tests.Surfaces;

/// <summary>Candidates and groups the surface tests share.</summary>
internal static class SurfaceTestData
{
    internal static SurfaceCandidate Candidate(string name, string? nodeKey = null, params string[] evidence) =>
        new(nodeKey ?? ("action:" + name.ToLowerInvariant().Replace(' ', '.')), "action", name, "actions", evidence.Length > 0 ? evidence : ["src/" + name.ToLowerInvariant().Replace(' ', '-') + "/grant.ts"], ["on surface:billing", "writes thing:account"]);

    internal static SurfaceGroup Group(params string[] names) => new([.. names.Select(n => Candidate(n))]);

    /// <summary>N groups of one candidate each, with names that sort in the order given.</summary>
    internal static List<SurfaceGroup> Groups(int count) => [.. Enumerable.Range(0, count).Select(i => Group("Surface " + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)))];
}
