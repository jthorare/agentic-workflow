using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.AddRepo;

[Lane(TestLane.Integration)]
public sealed class YamlProfileRepoWriterContractTests : ProfileRepoWriterContract, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-repo-writer-contract-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    protected override IProfileRepoWriter Create() => new YamlProfileRepoWriter(new SindriSettings(_dir, Path.Combine(_dir, "profile", "profile.yaml")));
}
