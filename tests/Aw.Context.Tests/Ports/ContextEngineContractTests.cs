using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.QueryContext;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class ContextEngineContractTests : ContextEngineContract<IContextEngine>
{
    protected override string ScopeGraphCodeWithNoMap => ContextErrors.NoMap.Code;

    protected override bool UnknownRunReadsAreEmpty => true;

    protected override IContextEngine CreateDown()
    {
        var docs = FakeContextSource.FixtureDocs();
        docs.Failure = ContextErrors.SourceUnavailable.With("down");
        return Engines.Over(docs);
    }
}
