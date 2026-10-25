using Aw.Context.Core.Graph;
using Aw.Context.Core.Search;
using Aw.Sindri.Host.Commands;
using Trust = Aw.Context.Core.Results.Trust;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The plain text of 'sindri context search': a counts line, then one line per hit with its snippet, an untrusted label fenced for a human at the terminal.</summary>
public sealed class ContextSearchTextTests
{
    private static MapSearchView View() => new("v3", Total: 12, Hits:
    [
        new MapSearchHit(NodeKey.From("feature:tabs.tab-manager"), NodeKind.Feature, "Tab manager", "aw-split-panes", "feature:tabs.tab-manager", 3.2, "title", "Tab manager", Trust.Trusted),
        new MapSearchHit(NodeKey.From("record:linear:issue/1"), NodeKind.Record, "Tab manager spec", Repo: null, "feature:tabs.tab-manager", 1.1, "text", "the spec of the tab manager", Trust.Untrusted),
    ]);

    [Fact, Trait("Lane", "unit")]
    public void The_text_says_how_many_of_how_many_and_lists_each_hit_with_the_field_that_matched()
    {
        var text = ContextSearchVerb.Text(View(), "tab manager", fence: false);

        Assert.StartsWith("search 'tab manager' in v3: 2 of 12 matches", text, StringComparison.Ordinal);
        Assert.Contains("feature:tabs.tab-manager  Tab manager  [title]", text, StringComparison.Ordinal);
        Assert.Contains("record:linear:issue/1  Tab manager spec  [text]", text, StringComparison.Ordinal);
        Assert.Contains("      the spec of the tab manager", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_untrusted_label_is_fenced_only_for_a_caller_who_reads_it_unfenced_from_the_server()
    {
        var fenced = ContextSearchVerb.Text(View(), "tab", fence: true);
        var plain = ContextSearchVerb.Text(View(), "tab", fence: false);

        Assert.Contains("<untrusted", fenced, StringComparison.Ordinal);
        Assert.DoesNotContain("<untrusted", plain, StringComparison.Ordinal);
    }
}
