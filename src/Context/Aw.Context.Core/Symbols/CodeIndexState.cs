using Aw.Context.Core.Code;
namespace Aw.Context.Core.Symbols;

/// <summary>What the code index block says about one repo (code index spec §7.3), in the block's own words; <see cref="CodeIndexReading"/> maps them onto the lens' vocabulary.</summary>
public enum CodeIndexState
{
    /// <summary>Built for the commit asked about, every layer that is on is answering.</summary>
    Ok,

    /// <summary>Answers come from an older commit than the checkout's head, or than the map's (as-of).</summary>
    Stale,

    /// <summary>The checkout is not on main: the old index is served.</summary>
    CheckoutNotOnMain,

    /// <summary>Built, but a layer (the graph) is unavailable: structure-only callers and callees.</summary>
    LayerUnavailable,

    /// <summary>No index file yet.</summary>
    Missing,

    /// <summary>The file is damaged; the next tick rebuilds it.</summary>
    Damaged,

    /// <summary>The first build is running (no old index to serve).</summary>
    Building,

    /// <summary>The repo has no approved profile entry for the index.</summary>
    NotConfigured,
}
