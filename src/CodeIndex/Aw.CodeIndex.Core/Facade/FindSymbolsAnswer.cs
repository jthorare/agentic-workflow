namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to FindSymbols.</summary>
/// <param name="Stamp">Which index answered.</param>
/// <param name="Hits">Ordered, at most the query's limit.</param>
/// <param name="Truncated">True when more matched than were returned.</param>
public sealed record FindSymbolsAnswer(AnswerStamp Stamp, IReadOnlyList<SymbolHit> Hits, bool Truncated)
{
    /// <summary>Plain-words lines about how the answer was scoped, set by the host (a concept scope's name and map, and where the index and map disagree); empty for an unscoped search.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}
