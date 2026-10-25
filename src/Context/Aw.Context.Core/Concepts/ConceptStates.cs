namespace Aw.Context.Core.Concepts;

/// <summary>The states of a concepts answer.</summary>
public static class ConceptStates
{
    /// <summary>The map has concepts.</summary>
    public const string Ok = "ok";

    /// <summary>The map has no concept yet: every feature, record and decision is in 'other'.</summary>
    public const string Empty = "empty";

    /// <summary>There is no map yet, and a build is running.</summary>
    public const string Building = "building";

    /// <summary>There is no map and no build: the answer's code says why.</summary>
    public const string Unavailable = "unavailable";
}
