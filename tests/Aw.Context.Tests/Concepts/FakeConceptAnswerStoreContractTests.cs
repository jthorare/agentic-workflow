using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Concepts;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class FakeConceptAnswerStoreContractTests : ConceptAnswerStoreContract
{
    protected override (IGraphStore Graphs, IConceptAnswerStore Answers) Create()
    {
        var graphs = new FakeGraphStore();
        return (graphs, new FakeConceptAnswerStore(graphs, new FakeIntentStore()));
    }
}
