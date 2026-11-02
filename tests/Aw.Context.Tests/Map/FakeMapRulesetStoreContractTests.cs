using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeMapRulesetStoreContractTests : MapRulesetStoreContract
{
    protected override IGraphStore CreateStore() => new FakeGraphStore();
}
