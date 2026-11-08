namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>The answer to "does this name exist outside the document".</summary>
public enum GraphLookup
{
    /// <summary>It exists.</summary>
    Known = 1,

    /// <summary>It doesn't.</summary>
    Unknown,

    /// <summary>Nobody can answer yet (no profile read, or connections and memory not built): the validator warns instead of refusing.</summary>
    Unavailable,
}
