using Aw.Context.Infrastructure.Docs;

namespace Aw.Context.Tests.Infrastructure;

public sealed class StoredDocsTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_document_cleaned_again_but_blocked_is_not_counted_as_recleaned()
    {
        var stored = new StoredDocs(new Dictionary<string, StoredDoc>(StringComparer.Ordinal)
        {
            ["docs/a.md"] = new("docs/a.md", "b1", Ruleset: ""),
        });

        Assert.True(stored.Recleaned(new("docs/a.md", "b1", "A", Blocked: false, [])));
        Assert.False(stored.Recleaned(new("docs/a.md", "b1", "A", Blocked: true, [])));
        Assert.False(stored.Recleaned(new("docs/a.md", "b2", "A", Blocked: false, [])));
    }
}
