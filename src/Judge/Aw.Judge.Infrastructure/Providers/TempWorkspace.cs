namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A fresh empty temporary directory, deleted with everything in it on dispose (best effort).</summary>
internal sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string path)
    {
        Path = path;
    }

    /// <summary>The directory.</summary>
    public string Path { get; }

    /// <summary>Creates one.</summary>
    /// <returns>The workspace.</returns>
    public static TempWorkspace Create() => new(Directory.CreateTempSubdirectory("judge-cli-").FullName);

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is harmless; the answer stands.
        }
    }
}
