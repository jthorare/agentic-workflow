using Aw.Kernel.Testing;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

// The adapter on a machine with no OS store (the owner-only file fallback), read back by the real pointer resolver.
[Lane(TestLane.Integration)]
public sealed class KeychainSecretWriterContractTests : SecretWriterContract, IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "aw-secret-contract-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    protected override (ISecretWriter Writer, ISecretPointers Pointers) Create()
    {
        var runner = new FakeToolRunner(ToolRun.Missing);
        return (new KeychainSecretWriter(runner, new FakeSindriHome(_home), KeychainPlatform.Unsupported), new PointerSecrets(static _ => null, runner, KeychainPlatform.Unsupported));
    }
}
