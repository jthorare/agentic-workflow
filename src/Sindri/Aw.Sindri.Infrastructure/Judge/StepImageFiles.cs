using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Infrastructure.Judge;

/// <summary><see cref="IStepImageFiles"/> over the file system: a file's or directory's final symlink target, else its full path.</summary>
internal sealed class StepImageFiles : IStepImageFiles
{
    /// <inheritdoc/>
    public string? RealPath(string path) => File.Exists(path) ? Real(new FileInfo(path)) : null;

    /// <inheritdoc/>
    public string RealDirectory(string directory) => Real(new DirectoryInfo(directory));

    // The final target of a link (the file's or the directory's own), else the full path; the parts above a link are the caller's own.
    private static string Real(FileSystemInfo info)
    {
        try
        {
            return (info.ResolveLinkTarget(returnFinalTarget: true) ?? info).FullName;
        }
        catch (IOException)
        {
            return info.FullName;   // boundary: an unreadable link counts as itself
        }
    }
}
