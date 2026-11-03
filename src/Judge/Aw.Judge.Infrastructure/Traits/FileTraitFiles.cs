using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Images;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>ITraitFiles over the file system: the process's working directory and UTF-8 text files.</summary>
/// <param name="workingDirectory">The directory the command runs in.</param>
/// <param name="images">Reads an edited image file; the file-system reader when null.</param>
internal sealed class FileTraitFiles(string workingDirectory, IImageReader? images = null) : ITraitFiles
{
    /// <inheritdoc />
    public string WorkingDirectory => workingDirectory;

    /// <inheritdoc />
    public string? ReadText(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            return text.Contains('\0', StringComparison.Ordinal) ? null : text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public long? FileSize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask<ImageRead> ReadImage(string path, string root, CancellationToken cancellationToken) =>
        (images ?? new FileImageReader()).Read([path], root, cancellationToken);
}
