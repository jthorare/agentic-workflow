using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class ContainerRuntimeFactsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("Docker Desktop", true)]
    [InlineData("docker desktop", true)]
    [InlineData("OrbStack", true)]
    [InlineData("Rancher Desktop", true)]
    [InlineData("Ubuntu 24.04.1 LTS", false)]
    [InlineData("Debian GNU/Linux 12 (bookworm)", false)]
    [InlineData("Fedora Linux 41 (Server Edition)", false)]
    [InlineData(null, false)]
    public void Docker_data_lives_in_a_vm_for_a_VM_runtime_and_on_the_host_for_a_native_engine(string? operatingSystem, bool inVm)
        => Assert.Equal(inVm, (FakeContainerRuntime.Healthy with { OperatingSystem = operatingSystem }).DataInVm);
}
