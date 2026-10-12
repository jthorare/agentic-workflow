using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class RulesProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => new RulesProvider();

    protected override IModelProvider? Answering(string decision) => null;

    protected override IModelProvider? Failing() => null;

    protected override IModelProvider? Hanging() => null;
}
