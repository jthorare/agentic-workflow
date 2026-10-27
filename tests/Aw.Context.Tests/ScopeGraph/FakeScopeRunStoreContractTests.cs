using Aw.Context.Core.ScopeGraph;
using Aw.Context.Testing.ScopeGraph;

namespace Aw.Context.Tests.ScopeGraph;

[Trait("Lane", "unit")]
public sealed class FakeScopeRunStoreContractTests : ScopeRunStoreContract
{
    protected override IScopeRunStore CreateStore() => new FakeScopeRunStore();
}
