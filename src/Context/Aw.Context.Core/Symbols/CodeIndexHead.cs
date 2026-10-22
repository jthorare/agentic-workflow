using Aw.Context.Core.Code;
namespace Aw.Context.Core.Symbols;

/// <summary>What the index holds for one repo and how it compares with the map's commit: the answer of <see cref="ICodeSymbolSource.Heads"/> and the header of every other answer.</summary>
/// <param name="State">The block's state, already counting an as-of mismatch (<see cref="CodeIndexReading.Head"/>).</param>
/// <param name="Commit">The commit the index describes, or null when there is no readable index.</param>
/// <param name="BuiltAt">When it was built, or null.</param>
/// <param name="AsOfCommit">The map version's commit the caller asked about, or null when it asked about none.</param>
/// <param name="Layer">The layer that is unavailable ('graph'), or null.</param>
/// <param name="Code">The block's SND-CODEIDX code that explains the state, or null.</param>
public sealed record CodeIndexHead(CodeIndexState State, string? Commit, DateTimeOffset? BuiltAt, string? AsOfCommit, string? Layer, string? Code)
{
    /// <summary>No readable index and nothing to say about why: the head of a repo the code index has no file for, and of the source Context registers until Sindri's Host replaces it.</summary>
    public static CodeIndexHead None { get; } = new(CodeIndexState.Missing, Commit: null, BuiltAt: null, AsOfCommit: null, Layer: null, Code: null);
}
