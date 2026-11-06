using Aw.Judge.Core.Images;

namespace Aw.Judge.Core.Ports;

/// <summary>The files trait-check reads besides the traits: the process's working directory and the repo's text files.</summary>
public interface ITraitFiles
{
    /// <summary>The directory the command runs in; relative paths and the repo are found from here.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The text, or null when the file is missing or can't be read, or is binary (holds a NUL).</returns>
    public string? ReadText(string path);

    /// <summary>Whether a directory exists (an edited path's directory may be gone, or not made yet).</summary>
    /// <param name="path">The directory's path.</param>
    /// <returns>True when it exists and is a directory.</returns>
    public bool DirectoryExists(string path);

    /// <summary>The size of a file from its metadata, without reading it.</summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The size in bytes, or null when the file is missing or can't be examined.</returns>
    public long? FileSize(string path);

    /// <summary>Reads one PNG or JPEG file (an edited screenshot) as an image.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="root">The checkout; the file's real path (symlinks resolved) must stay under it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The image, or why it can't be read (missing, not a PNG or JPEG, outside the checkout).</returns>
    public ValueTask<ImageRead> ReadImage(string path, string root, CancellationToken cancellationToken);
}
