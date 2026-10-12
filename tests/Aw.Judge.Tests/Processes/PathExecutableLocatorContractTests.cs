using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Integration)]
public sealed class PathExecutableLocatorContractTests : ExecutableLocatorContract, IDisposable
{
    private readonly PathDirectories _path = new();

    public void Dispose() => _path.Dispose();

    protected override IExecutableLocator Create(IReadOnlyList<string> installed)
    {
        for (var i = 0; i < installed.Count; i++)
        {
            PathDirectories.AddFile(i % 2 == 0 ? _path.First : _path.Second, installed[i], executable: true);
        }

        return new PathExecutableLocator(name => name == "PATH" ? _path.Path : null);
    }
}
