using System.Text.Json.Serialization;
using Aw.Context.Core.Symbols;

namespace Aw.Context.Core.Lenses;

/// <summary>The symbol column of the code band and of the file map: file-level only until the code index answers (Phase 9, S-9a).</summary>
/// <param name="State">'unavailable' when the index cannot answer, 'stale' when it answers from another commit than the map's or the head, else 'ok'.</param>
/// <param name="Reason">'index not built', 'index building', 'index damaged', 'stale', or null.</param>
/// <param name="Items">The symbols (none yet in the lenses' code band).</param>
public sealed record LensSymbols(SymbolsState State, string? Reason, IReadOnlyList<string> Items)
{
    /// <summary>What S-4 serves everywhere: no code index.</summary>
    public static LensSymbols Unavailable { get; } = new(SymbolsState.Unavailable, "index not built", []);

    /// <summary>The file map's column: the symbols each listed file defines (none for a file the index does not know), or null when the index cannot answer.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FileSymbolsRow>? Files { get; init; }

    /// <summary>The symbols that implement the map's nodes, the file-level links refined (Phase 9); null when the index cannot answer, so the links stay file-level and 'state' says why.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SymbolImplements>? Implements { get; init; }

    /// <summary>The commit the symbols were read at, or null when none were.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Commit { get; init; }

    /// <summary>The map version's commit when it differs from <see cref="Commit"/> ('from abcdef0, not 1234567'), else null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AsOfCommit { get; init; }

    /// <summary>True when the index and the map are at different commits.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AsOfMismatch { get; init; }

    /// <summary>The code that explains an unavailable answer, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }
}
