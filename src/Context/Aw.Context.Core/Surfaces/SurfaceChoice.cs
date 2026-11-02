namespace Aw.Context.Core.Surfaces;

/// <summary>What Jev decided for one group (spec §6.9).</summary>
public enum SurfaceChoice
{
    /// <summary>The candidates are different nodes; all stay (also the answer to anything uncertain).</summary>
    Separate,

    /// <summary>The candidates are one node, named with the chosen candidate's name.</summary>
    Merge,

    /// <summary>None of the candidates is a real product surface, thing, action or flow.</summary>
    Reject,
}
