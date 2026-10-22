using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Testing.Prove;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The in-memory artifact files honour the artifact files contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryArtifactFilesContractTests : ArtifactFilesContract
{
    private readonly InMemoryArtifactFiles _files = new();

    protected override string Workspace => "/work/phase-1";

    protected override IArtifactFiles Create() => _files;

    protected override void Put(string path, string text) => _files.With(Workspace, path, text);
}
