namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>A temp directory that is deleted when the test ends.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("egress").FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
