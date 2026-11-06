using System.Text.RegularExpressions;
using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocPathologicalTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_heading_of_spaces_then_a_letter_parses_without_a_regex_timeout()
    {
        var doc = ADoc.Parse("spec", "## Goals\n# a" + new string(' ', 100_000) + "x\n- **REQ-1** real\n");

        Assert.Contains("REQ-1", ADoc.Ids(doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_heading_of_open_brackets_parses_without_a_regex_timeout()
    {
        var doc = ADoc.Parse("spec", "## Goals\n## " + new string('[', 100_000) + "\n- **REQ-1** real\n");

        Assert.Contains("REQ-1", ADoc.Ids(doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_table_cell_of_repeated_doc_mentions_without_a_hash_parses_without_a_regex_timeout()
    {
        var doc = ADoc.Parse("spec", "## Goals\n| REQ-1 | " + string.Concat(Enumerable.Repeat("?doc:aaaa", 20_000)) + " |\n- **REQ-1** real\n");

        Assert.Contains("REQ-1", ADoc.Ids(doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parse_that_times_out_is_a_doc_with_error_018_and_no_items()
    {
        var doc = DesignDocParser.Guard(ADoc.Path, static () => throw new RegexMatchTimeoutException());

        Assert.Equal([DesignDocCodes.TooLarge], ADoc.Codes(doc));
        Assert.Empty(doc.Items);
        Assert.Empty(doc.Refs);
        Assert.True(DesignDocParser.IsDesignDoc(doc));
    }
}
