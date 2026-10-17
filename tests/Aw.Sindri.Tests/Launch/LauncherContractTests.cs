using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The real launcher over the fake host and viewer honours the same contract as <see cref="FakeLauncherContractTests"/>' fake.</summary>
[Lane(TestLane.Unit)]
public sealed class LauncherContractTests : LauncherContract, IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-").FullName;
    private readonly FakeSessionHost _host = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    protected override ILauncher Create() =>
        new Launcher(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, new FakeTerminalViewer(), new LauncherOptions(_home), new FakeTimeProvider(KnownInstants.Epoch)));

    protected override int SessionCount() => _host.LiveCount;
}
