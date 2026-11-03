namespace Aw.Judge.Tests.Images;

/// <summary>A fresh temporary directory, deleted with everything in it afterwards.</summary>
internal sealed class ScratchDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("judge-images-test-").FullName;

    /// <summary>Writes a file (creating its directories) and returns its absolute path.</summary>
    public string Write(string relative, byte[] bytes)
    {
        var path = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
