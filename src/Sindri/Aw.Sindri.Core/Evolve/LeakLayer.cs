namespace Aw.Sindri.Core.Evolve;

/// <summary>The layers of the leak gate, in the order they run. A hold names its layer and nothing else.</summary>
internal enum LeakLayer
{
    /// <summary>The rewrite changed the kind, the target or the evidence of the original.</summary>
    Rewrite,

    /// <summary>The judge said the rewrite still leaks.</summary>
    Judge,

    /// <summary>A name from privacy.denyTerms.</summary>
    Terms,

    /// <summary>A match of privacy.denyPatterns.</summary>
    Patterns,

    /// <summary>A name from a private repo.</summary>
    Identifiers,

    /// <summary>Text copied from a turn the model saw.</summary>
    Verbatim,

    /// <summary>An email address or a home path.</summary>
    Personal,

    /// <summary>The secret scrubber would change the text.</summary>
    Scrubber,
}
