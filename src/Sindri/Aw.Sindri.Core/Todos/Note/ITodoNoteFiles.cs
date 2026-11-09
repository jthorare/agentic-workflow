namespace Aw.Sindri.Core.Todos.Note;

/// <summary>
/// The note sync's files (todo spec §4.5 Writing steps 1-5, the base). Paths are absolute. Base and intent files live in Sindri's own todo folder
/// ('$AW_SINDRI_HOME/todo/', mode 0700, files 0600), which the adapter owns. Nothing here merges or decides; the pass does.
/// </summary>
public interface ITodoNoteFiles
{
    /// <summary>Reads the note: up to 1 MB, no symlink, at most one hard link.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>What was found.</returns>
    public NoteRead Read(string note);

    /// <summary>Reads <paramref name="path"/> once it has been unchanged (size, write time and hash) for <paramref name="quiet"/>, or after <paramref name="max"/> anyway.</summary>
    /// <param name="path">The note, or a file that came out of a swap.</param>
    /// <param name="quiet">How long it must stay unchanged (2 s).</param>
    /// <param name="max">The longest wait (30 s).</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The last read, and whether it settled before <paramref name="max"/>.</returns>
    public ValueTask<(NoteRead Read, bool Settled)> ReadQuiet(string path, TimeSpan quiet, TimeSpan max, CancellationToken cancellationToken);

    /// <summary>Step 1: records the hash a write is about to put in place (the intent file, replacing any older one).</summary>
    /// <param name="hash">The hash of the build.</param>
    public void WriteIntent(string hash);

    /// <summary>The hash in the intent file, or null when there is none.</summary>
    /// <returns>The hash.</returns>
    public string? ReadIntent();

    /// <summary>Step 4: removes the intent file.</summary>
    public void ClearIntent();

    /// <summary>
    /// Steps 1-2: writes <paramref name="text"/> to '.&lt;note name&gt;.sindri-tmp' beside the note (created exclusive, not following links, with the note's mode),
    /// then swaps it with the note in one call. A missing note is created from the tmp file without replacing anything that appeared meanwhile.
    /// </summary>
    /// <param name="note">The note's path.</param>
    /// <param name="text">The new note.</param>
    /// <param name="vaultRoot">The vault's folder: no folder from it down to the note may be a symlink.</param>
    /// <returns>Where what was at the note's path went, or why the write didn't happen.</returns>
    public NoteSwap Swap(string note, string text, string vaultRoot);

    /// <summary>The leftover tmp file beside the note from an earlier run, or null.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>Its path.</returns>
    public string? Leftover(string note);

    /// <summary>Deletes a tmp file once its content is merged or matched.</summary>
    /// <param name="path">The tmp file.</param>
    public void Delete(string path);

    /// <summary>The 0-byte exit (step 3): renames a came-out file to '.&lt;note name&gt;.sindri-tmp.keep-&lt;time&gt;' and returns that path.</summary>
    /// <param name="path">The came-out file.</param>
    /// <param name="at">The time in its name.</param>
    /// <returns>The new path.</returns>
    public string Keep(string path, DateTimeOffset at);

    /// <summary>Saves a base file named by its own hash ('note-base-&lt;hash&gt;.md', 0600).</summary>
    /// <param name="noteBase">The base lines.</param>
    /// <returns>The hash that names it.</returns>
    public string SaveBase(NoteBase noteBase);

    /// <summary>The base <paramref name="hash"/> names, or null when the file is missing or its hash doesn't match (no base).</summary>
    /// <param name="hash">The 'baseHash' of the last 'todo.note.synced'.</param>
    /// <returns>The base.</returns>
    public NoteBase? LoadBase(string hash);

    /// <summary>Deletes every base file but <paramref name="keep"/> (all of them when null: stopping the sync).</summary>
    /// <param name="keep">The base still in force.</param>
    public void DeleteBases(string? keep);
}
