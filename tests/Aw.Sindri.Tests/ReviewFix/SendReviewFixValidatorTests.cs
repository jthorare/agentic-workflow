using Aw.Kernel.Testing;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>The shape of a fix task (PR review page spec §8.2): a kind, 1 to 2,000 characters, a 40-digit head.</summary>
[Lane(TestLane.Unit)]
public sealed class SendReviewFixValidatorTests
{
    private static readonly string _sha = new('a', 40);

    private static SendReviewFixRequest Request(string kind = "check", string text = "CI is red.", string? head = null, string? side = null, int? line = null, string? path = null) =>
        new("acme", "api", 2071, kind, text, head ?? _sha, Source: "ci", path, line, side, Item: null);

    private static string? Code(SendReviewFixRequest request)
    {
        var result = new SendReviewFixValidator().Validate(request);
        return result.IsError ? result.Error.Code : null;
    }

    [Fact]
    public void A_good_request_passes() => Assert.Null(Code(Request()));

    [Fact]
    public void Text_of_2000_characters_passes_and_2001_is_refused()
    {
        Assert.Null(Code(Request(text: new string('x', 2000))));
        Expect.Equal("SND-CLI-003", Code(Request(text: new string('x', 2001))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_is_refused(string text) => Assert.NotNull(Code(Request(text: text)));

    [Fact]
    public void An_unknown_kind_a_short_head_and_an_unknown_side_are_refused()
    {
        Assert.NotNull(Code(Request(kind: "other")));
        Assert.NotNull(Code(Request(head: "abc123")));
        Assert.NotNull(Code(Request(side: "left")));
    }

    [Fact]
    public void A_line_needs_its_file() => Assert.NotNull(Code(Request(kind: "line", line: 4)));
}
