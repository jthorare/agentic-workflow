using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

// The files scripts/install-cs.sh writes: <build>/inputs/<name>.rev (one line) and <name>.paths (one pathspec per line).
[Lane(TestLane.Integration)]
public sealed class FileInstalledBuildsContractTests : InstalledBuildsContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override (IInstalledBuilds Builds, Action<string, string, IReadOnlyList<string>?> Put) Create()
    {
        var build = Path.Combine(_dir.Path, "current");
        return (new FileInstalledBuilds(build), (name, revision, inputs) =>
        {
            var folder = Path.Combine(build, "inputs");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name + ".rev"), revision + "\n");
            if (inputs is not null)
            {
                File.WriteAllText(Path.Combine(folder, name + ".paths"), string.Join('\n', inputs) + "\n");
            }
        }
        );
    }

    public void Dispose() => _dir.Dispose();
}
