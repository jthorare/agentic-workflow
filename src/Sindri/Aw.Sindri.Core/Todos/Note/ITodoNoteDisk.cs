namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The disk side of the note check (todo spec §4.5): a port, so the tests run on a fake and the real one is a P/Invoke.</summary>
public interface ITodoNoteDisk
{
    /// <summary>Whether the vault's folder is there.</summary>
    /// <param name="vaultRoot">The vault's folder.</param>
    /// <returns>True when the folder exists.</returns>
    public bool VaultExists(string vaultRoot);

    /// <summary>Looks at the note's file; changes nothing.</summary>
    /// <param name="vaultRoot">The vault's folder.</param>
    /// <param name="note">The note's path inside it.</param>
    /// <returns>What is there.</returns>
    public NoteFileFacts Inspect(string vaultRoot, string note);

    /// <summary>Probes the nearest existing folder on the way to the note: creates two temporary files there, tries the swap and removes both.</summary>
    /// <param name="vaultRoot">The vault's folder.</param>
    /// <param name="note">The note's path inside it.</param>
    /// <returns>Whether the folder can be written and swapped in.</returns>
    public NoteFolderFacts Probe(string vaultRoot, string note);
}
