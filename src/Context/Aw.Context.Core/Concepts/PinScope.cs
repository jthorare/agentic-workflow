using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>One pin as a query sees it: what is inside it (concept pins spec §3.1).</summary>
public sealed class PinScope
{
    private readonly object _stub = new();

    private PinScope()
    {
    }

    /// <summary>The pin.</summary>
    public ConceptId Pin
    {
        get
        {
            _ = _stub;
            throw new NotSupportedException();
        }
    }

    /// <summary>The pin <paramref name="pin"/> names, as the caller may see it.</summary>
    /// <param name="mapped">The repos the map is built from.</param>
    /// <param name="visible">The caller's repos.</param>
    /// <param name="pin">A pin id or 'concept:&lt;id&gt;'.</param>
    /// <param name="graph">The map's whole stored graph.</param>
    /// <param name="mapTag">How answers name the map ('v41').</param>
    /// <returns>The scope, or SND-CONTEXT-102, 103 or 104.</returns>
    public static Result<PinScope> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, string pin, GraphSnapshot graph, string mapTag) => throw new NotSupportedException();

    /// <summary>Whether a repo file is in this pin: the first pin of that repo whose paths match holds it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="repoRelativePath">The path in the repo.</param>
    /// <returns>True when the pin holds it.</returns>
    public bool HoldsPath(ContextRepoName repo, string repoRelativePath)
    {
        _ = (_stub, repo, repoRelativePath);
        throw new NotSupportedException();
    }

    /// <summary>Whether a node is in this pin.</summary>
    /// <param name="key">The node.</param>
    /// <returns>True when the pin holds it.</returns>
    public bool HoldsNode(NodeKey key)
    {
        _ = (_stub, key);
        throw new NotSupportedException();
    }

    /// <summary>The pin an anchor sits in.</summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="mapped">The repos the map is built from.</param>
    /// <param name="graph">The map's graph.</param>
    /// <returns>The pin, or null.</returns>
    public static ConceptId? PinOf(NodeKey anchor, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph) => throw new NotSupportedException();
}
