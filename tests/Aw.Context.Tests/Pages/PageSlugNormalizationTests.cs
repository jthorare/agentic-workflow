using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Pages;

/// <summary>One visible name gets one id in NFC or NFD, and a lone surrogate does not throw (#1823).</summary>
public sealed class PageSlugNormalizationTests
{
    [Fact, Trait("Lane", "unit")]
    public void Nfd_and_nfc_spellings_get_one_id() =>
        Assert.Equal(PageSlugs.Of("Café"), PageSlugs.Of("Café"));

    [Fact, Trait("Lane", "unit")]
    public void A_lone_surrogate_still_gets_a_valid_id()
    {
        var id = PageSlugs.Of("日\ud800");

        Assert.True(PageId.TryFrom(id, out _));
    }
}
