using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>Review of PR 3881: a kept answer's key holds the running redactor's rules digest, so a toolkit upgrade that changes a redaction rule misses instead of answering with text the new rules would scrub.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerKeyTests
{
    private static readonly ConceptAnswerBasis Basis = new(3, "0:0", 7);
    private static readonly ContextScope App = MapWorld.HumanVia(ContextClient.Http);

    [Fact]
    public void A_changed_rules_digest_misses_and_the_same_one_hits()
    {
        var query = new ConceptsQuery(MapRead.Current, new ConceptPage());
        var glossary = new ConceptGlossaryQuery(MapRead.Current);

        Assert.Equal(ConceptAnswerKey.Concepts(Basis, query, App, "a1b2c3d4e5f60718"), ConceptAnswerKey.Concepts(Basis, query, App, "a1b2c3d4e5f60718"));
        Assert.NotEqual(ConceptAnswerKey.Concepts(Basis, query, App, "a1b2c3d4e5f60718"), ConceptAnswerKey.Concepts(Basis, query, App, "ffffffffffffffff"));
        Assert.NotEqual(ConceptAnswerKey.Glossary(Basis, glossary, App, "a1b2c3d4e5f60718"), ConceptAnswerKey.Glossary(Basis, glossary, App, "ffffffffffffffff"));
    }
}
