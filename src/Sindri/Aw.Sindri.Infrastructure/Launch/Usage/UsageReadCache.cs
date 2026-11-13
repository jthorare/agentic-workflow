namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// Keeps a usage reader's per-transcript read state on disk (#3420), so a restarted serve resumes each transcript where the last run stopped
/// instead of reading today's files from byte 0. Not built yet: <see cref="Load"/> finds nothing and <see cref="Save"/> keeps nothing.
/// </summary>
/// <param name="directory">The directory the state files live in.</param>
internal sealed class UsageReadCache(string directory)
{
    /// <summary>The directory the state files live in.</summary>
    public string Directory { get; } = directory;

    /// <summary>The saved state of <paramref name="reader"/>, per transcript path.</summary>
    /// <param name="reader">The reader's name.</param>
    /// <returns>The saved states; empty when there are none.</returns>
    public IReadOnlyDictionary<string, FileState> Load(string reader) => reader.Length == 0 || Directory.Length == 0 ? throw new ArgumentException("A reader name is required.", nameof(reader)) : new Dictionary<string, FileState>(StringComparer.Ordinal);

    /// <summary>Keeps the state of <paramref name="reader"/>.</summary>
    /// <param name="reader">The reader's name.</param>
    /// <param name="files">The state per transcript path.</param>
    public void Save(string reader, IReadOnlyDictionary<string, FileState> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(reader);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(Directory);
    }
}
