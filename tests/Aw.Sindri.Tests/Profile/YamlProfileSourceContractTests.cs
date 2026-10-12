using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

[Lane(TestLane.Integration)]
public sealed class YamlProfileSourceContractTests : ProfileSourceContract, IDisposable
{
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    protected override IProfileSource Create() => _files.Source;

    protected override void PutValid() => _files.Write("schemaVersion: 1\nuser: thor\n");

    protected override void PutInvalid() => _files.Write("schemaVersion: 1\nuser: thor\nunknownKey: 1\n");

    protected override void PutNone()
    {
        if (File.Exists(_files.Settings.ProfilePath))
        {
            File.Delete(_files.Settings.ProfilePath);
        }
    }
}
