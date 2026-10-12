using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Integration)]
public sealed class FileEndpointContractTests : EndpointFileContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private SindriSettings Settings => new(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));

    protected override IEndpointFile Create() => new FileEndpoint(Settings, SliceHarness.Json);

    protected override void Damage(IEndpointFile file) => File.WriteAllText(Settings.EndpointPath, "{\"url\": ");
}
