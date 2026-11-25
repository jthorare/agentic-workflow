using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the symbol lens says: a state word, a reason and the items; since S-9 also the commit it was read from (the code index spec §7.3).</summary>
/// <param name="State">State, as served.</param>
/// <param name="Reason">Reason, as served.</param>
/// <param name="Items">Items, as served.</param>
/// <param name="Commit">The commit the index describes, when there is a readable index.</param>
/// <param name="AsOfCommit">The map version's commit, when the index is at another one.</param>
/// <param name="AsOfMismatch">Whether the index and the map are at different commits.</param>
/// <param name="Unresolved">How many call sites the index could not tie to a symbol (callers and callees only).</param>
/// <param name="Code">The block's SND-CODEIDX code that explains the state.</param>
/// <param name="Files">The symbols each listed file defines (the file map only).</param>
/// <param name="Repos">The lenses code band only: with several repos in scope and no answer, the repos whose index gives that state (#3861); null otherwise.</param>
internal sealed record SymbolsView(
    string State,
    string? Reason,
    IReadOnlyList<JsonElement> Items,
    string? Commit = null,
    string? AsOfCommit = null,
    bool AsOfMismatch = false,
    int Unresolved = 0,
    string? Code = null,
    IReadOnlyList<FileSymbolsEntry>? Files = null,
    IReadOnlyList<string>? Repos = null);
