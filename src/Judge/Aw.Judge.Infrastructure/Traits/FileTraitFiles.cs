using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>ITraitFiles over the file system: the process's working directory and UTF-8 text files.</summary>
/// <param name="workingDirectory">The directory the command runs in.</param>
internal sealed class FileTraitFiles(string workingDirectory) : ITraitFiles
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
}
