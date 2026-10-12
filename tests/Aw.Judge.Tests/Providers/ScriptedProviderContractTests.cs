using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ScriptedProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => new ScriptedProvider(ProviderName.ClaudeCli).Decides("send", 1);

    protected override IModelProvider? Answering(string decision) => new ScriptedProvider(ProviderName.ClaudeCli).Decides(decision, 1);

    protected override IModelProvider? Failing() => new ScriptedProvider(ProviderName.ClaudeCli).Returns(new ProviderErrored("exit-1"));

    protected override IModelProvider? Hanging() => new ScriptedProvider(ProviderName.ClaudeCli).Returns(new ProviderUnavailable(ProviderUnavailable.Timeout));
}
