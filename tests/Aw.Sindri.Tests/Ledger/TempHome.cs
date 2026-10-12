namespace Aw.Sindri.Tests.Ledger;

/// <summary>A throwaway Sindri home under the temp directory, deleted on dispose.</summary>
public sealed class TempHome : IDisposable
{
    public TempHome()
    {
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-sindri-" + System.IO.Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
