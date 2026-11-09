namespace Aw.Sindri.Core.Todos.Note;

/// <summary>What a probe of the note's folder found.</summary>
/// <param name="Writable">Whether Sindri can create a file in the folder.</param>
/// <param name="CanSwap">Whether two files in the folder can be swapped in one step (renameat2 RENAME_EXCHANGE on Linux, renamex_np RENAME_SWAP on macOS); false on a disk without the call.</param>
public sealed record NoteFolderFacts(bool Writable, bool CanSwap);
