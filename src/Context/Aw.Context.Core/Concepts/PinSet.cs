using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Every pin the caller can see, with the node and path each one holds (concept pins spec §3.1).</summary>
public sealed class PinSet
{
    private readonly object _stub = new();

    private PinSet()
    {
    }

    /// <summary>No pins.</summary>
    public static PinSet Empty { get; } = new();

    /// <summary>True when the caller sees no pin.</summary>
    public bool IsEmpty
    {
        get
        {
            _ = _stub;
            throw new NotSupportedException();
        }
    }

    /// <summary>The ids of the visible pins, in the order the map builds them.</summary>
    public IReadOnlyList<ConceptId> Visible
    {
        get
        {
            _ = _stub;
            throw new NotSupportedException();
        }
    }

    /// <summary>The pins of a map.</summary>
    /// <param name="mapped">The repos the map is built from; the ids are made over these.</param>
    /// <param name="visible">The caller's repos.</param>
    /// <param name="graph">The map's graph.</param>
    /// <returns>The set.</returns>
    public static PinSet Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, GraphSnapshot graph) => throw new NotSupportedException();

    /// <summary>The pin a node sits in: the first in pin order when several claim it.</summary>
    /// <param name="key">The node.</param>
    /// <returns>The pin, or null.</returns>
    public ConceptId? PinOf(NodeKey key)
    {
        _ = (_stub, key);
        throw new NotSupportedException();
    }
}
