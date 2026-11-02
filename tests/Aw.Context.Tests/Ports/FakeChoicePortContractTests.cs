using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeChoicePortContractTests : ChoicePortContract<FakeChoicePort>
{
    protected override FakeChoicePort Create() => new();
}
