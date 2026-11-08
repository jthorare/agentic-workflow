namespace Aw.Sindri.Core.Memory;

/// <summary>How close a fact has to be to the query to be recalled: the cosine floor of semantic recall.</summary>
public enum MatchLevel
{
    /// <summary>Anything remotely related.</summary>
    Loose = 1,

    /// <summary>The default: clearly related.</summary>
    Balanced = 2,

    /// <summary>Close to a paraphrase.</summary>
    Strict = 3,
}
