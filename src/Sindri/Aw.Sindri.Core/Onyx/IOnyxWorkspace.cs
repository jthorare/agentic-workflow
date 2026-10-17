using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The files `sindri onyx up` keeps under the Sindri home: the rendered .env and compose override, and the setup state.</summary>
public interface IOnyxWorkspace
{
    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file's path (one of <see cref="OnyxPaths"/>).</param>
    /// <returns>Its text, null when it doesn't exist, or SND-ONYX-012 when it exists and can't be read (no permission, an I/O error).</returns>
    public Result<string?> Read(string path);

    /// <summary>Writes a text file only when its content differs, atomically (a temporary file renamed over the target, so a reader sees the old or the new content, never a partial one).</summary>
    /// <param name="path">The file's path; missing parent directories are created.</param>
    /// <param name="content">The text.</param>
    /// <param name="secret">The file holds a secret: it is created owner-only (0600) from the start, in a directory that is owner-only (0700) when it is created here.</param>
    /// <returns>True when it wrote, false when the file already had this content.</returns>
    public bool WriteIfChanged(string path, string content, bool secret);

    /// <summary>Reads the setup state.</summary>
    /// <returns>The state, or null when the file is missing or corrupt (never throws: a corrupt state is an empty one, every step is re-checked and each is idempotent).</returns>
    public OnyxSetupState? ReadState();

    /// <summary>Tells a state file that exists and can't be read as state (corrupt, empty, or written for another schema version) from a missing one.</summary>
    /// <returns>True when the file exists and <see cref="ReadState"/> returns null for it; false when it is missing or reads fine.</returns>
    public bool StateIsUnreadable();

    /// <summary>Writes the setup state, atomically and owner-only.</summary>
    /// <param name="state">The state.</param>
    public void WriteState(OnyxSetupState state);

    /// <summary>Takes the exclusive lock of the onyx directory, so one `onyx up` runs at a time (a second one is refused, not queued).</summary>
    /// <returns>The held lock (dispose it to release), null when another holder has it, or SND-ONYX-012 when the directory can't be used (no permission).</returns>
    public Result<IDisposable?> TryLock();
}
