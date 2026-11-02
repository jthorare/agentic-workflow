using Aw.Context.Core.ConceptScan;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeConceptHintStoreContractTests : ConceptHintStoreContract
{
    protected override IConceptHintStore CreateStore() => new FakeConceptHintStore();
}
