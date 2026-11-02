using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;

namespace Aw.Context.Tests.Pages;

/// <summary>Page names made only of non-Latin letters get their own stable, valid ids instead of the bare 'page' (#1823).</summary>
public sealed class NonLatinPageIdsTests
{
    private static IReadOnlyList<PageListRow> Preview(string paste) =>
        PageListPreview.Of(PageListText.Parse(paste), [], []);

    [Fact, Trait("Lane", "unit")]
    public void Two_non_latin_names_get_different_valid_ids()
    {
        var japanese = PageSlugs.Of("日本語");
        var chinese = PageSlugs.Of("中文");

        Assert.NotEqual(japanese, chinese, StringComparer.Ordinal);
        Assert.NotEqual("page", japanese, StringComparer.Ordinal);
        Assert.NotEqual("page", chinese, StringComparer.Ordinal);
        Assert.True(PageId.TryFrom(japanese, out _));
        Assert.True(PageId.TryFrom(chinese, out _));
        Assert.True(PageActionId.TryFrom(japanese, out _));
        Assert.True(PageActionId.TryFrom(chinese, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_non_latin_id_is_stable() =>
        Assert.Equal(PageSlugs.Of("日本語"), PageSlugs.Of("日本語"));

    [Fact, Trait("Lane", "unit")]
    public void A_paste_of_two_non_latin_pages_adds_both()
    {
        var rows = Preview("日本語\n\n中文");

        Assert.Equal(2, rows.Count);
        Assert.Null(rows[1].SameAs);
        Assert.True(rows[0].Ticked);
        Assert.True(rows[1].Ticked);
        Assert.NotEqual(rows[0].Page!.Id, rows[1].Page!.Id);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_blank_name_still_gets_page() =>
        Assert.Equal("page", PageSlugs.Of("  "));

    [Fact, Trait("Lane", "unit")]
    public void Mixed_names_that_differ_only_in_non_latin_letters_get_different_ids()
    {
        var japanese = PageSlugs.Of("日本語 v2");
        var chinese = PageSlugs.Of("中文 v2");

        Assert.NotEqual(japanese, chinese, StringComparer.Ordinal);
        Assert.True(PageId.TryFrom(japanese, out _));
        Assert.True(PageId.TryFrom(chinese, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ascii_name_keeps_its_slug() =>
        Assert.Equal("schedule-week", PageSlugs.Of("Schedule week"));
}
