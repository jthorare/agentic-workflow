using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A host with no files unless the test lists them, and the uid the test names.</summary>
internal sealed class FakeHostPaths : IHostPaths
{
    internal int? RealUid { get; init; }

    internal Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public HostPathKind KindOf(string path) => HostPathKind.Missing;

    public int? Uid() => RealUid;

    public string? ReadText(string path) => Files.GetValueOrDefault(path);

    public string Resolve(string path) => path;
}
