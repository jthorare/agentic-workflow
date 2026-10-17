using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>A fresh directory under the system temp directory, deleted on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = RealPath.Of(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aw-meta-test-" + RealTime.NewGuid().ToString("N")));
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
