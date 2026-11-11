using Aw.Meta.Core;
using Aw.Meta.Core.Defects;
using Aw.Meta.Features.ReportDefect;

namespace Aw.Meta.Tests.ReportDefect;

/// <summary>#2715: the duplicate search is `"&lt;code&gt;: &lt;title&gt;" in:title`, so a double quote in --title would end the phrase early and a twin issue could be filed. The validator refuses it with AW-META-001.</summary>
public sealed class ReportDefectTitleQuoteTests
{
    private static ReportDefectRequest Defect(string title) =>
        new("AW-META-010", title, "lane-recycle exited 1 on a busy lane", "AW-META-010: recycle failed", Path: null);

    [Theory, Trait("Lane", "unit")]
    [InlineData("the \"x\" flag breaks recycle")]
    [InlineData("recycle fails on \"")]
    [InlineData("\"quoted\"")]
    public void A_title_with_a_double_quote_is_refused_with_AW_META_001(string title)
    {
        var result = new ReportDefectValidator().Validate(Defect(title));

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("double quote", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("recycle fails")]
    [InlineData("the 'x' flag breaks recycle")]
    [InlineData("the \"x\" flag breaks recycle")]
    public void Every_title_the_validator_accepts_makes_a_search_phrase_with_no_inner_quote(string title)
    {
        if (new ReportDefectValidator().Validate(Defect(title)).IsError)
        {
            return;
        }

        Assert.DoesNotContain('"', DefectText.Title("AW-META-010", title));
    }
}
