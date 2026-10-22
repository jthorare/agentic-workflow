using System.Text.Json.Serialization;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Core.Code;

/// <summary>Callers or callees of the code lens: a recorded state when the code index cannot answer, else the edges (S-9a).</summary>
/// <param name="State">'unavailable' when the index cannot answer, 'stale' when it answers from another commit than the map's or the head, else 'ok'.</param>
/// <param name="Reason">'index-not-built', 'index-damaged', 'index-building', 'stale', 'checkout-not-on-main', or a note on an answer that is ok ('graph-unavailable'); null when there is nothing to say.</param>
/// <param name="Items">The edges (at most 100).</param>
public sealed record CodeSymbols(SymbolsState State, string? Reason, IReadOnlyList<CallEdgeRow> Items)
{
    /// <summary>The reason word of a missing code index.</summary>
    public const string IndexNotBuilt = "index-not-built";

    /// <summary>What the lens serves when no code index answers.</summary>
    public static CodeSymbols Unavailable { get; } = new(SymbolsState.Unavailable, IndexNotBuilt, []);

    /// <summary>The commit the edges were read at, or null when none were.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Commit { get; init; }

    /// <summary>The map version's commit when it differs from <see cref="Commit"/> ('from abcdef0, not 1234567'), else null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AsOfCommit { get; init; }

    /// <summary>True when the index and the map are at different commits.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AsOfMismatch { get; init; }

    /// <summary>The call sites the index could not tie to a symbol.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Unresolved { get; init; }

    /// <summary>The SND-CODEIDX or SND-CONTEXT code that explains an unavailable answer, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }
}
