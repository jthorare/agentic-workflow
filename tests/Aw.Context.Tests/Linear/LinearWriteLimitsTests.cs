using Aw.Context.Core.Linear;

namespace Aw.Context.Tests.Linear;

[Trait("Lane", "unit")]
public sealed class LinearWriteLimitsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_title_of_whitespace_only_is_refused(string title) => AssertNotSent(LinearWriteLimits.Title(title));

    [Fact]
    public void A_title_of_one_to_255_characters_passes()
    {
        Assert.Null(LinearWriteLimits.Title("a"));
        Assert.Null(LinearWriteLimits.Title(new string('t', 255)));
        AssertNotSent(LinearWriteLimits.Title(new string('t', 256)));
    }

    [Fact]
    public void A_change_that_sets_no_field_is_refused_with_its_own_words()
    {
        var error = LinearWriteLimits.Change(new LinearIssueChange());

        Assert.NotNull(error);
        Assert.Equal("SND-LINEAR-015", error.Code);
        Assert.Equal("Not sent: the change sets no field. Nothing was changed.", error.Message);
    }

    [Fact]
    public void A_change_checks_its_title_and_description()
    {
        AssertNotSent(LinearWriteLimits.Change(new LinearIssueChange(Title: LinearFieldChange.Set(" "))));
        AssertNotSent(LinearWriteLimits.Change(new LinearIssueChange(Description: LinearFieldChange.Set<string?>(new string('x', 65_537)))));
        Assert.Null(LinearWriteLimits.Change(new LinearIssueChange(Description: LinearFieldChange.Set<string?>(null))));
        Assert.Null(LinearWriteLimits.Change(new LinearIssueChange(Priority: LinearFieldChange.Set(0))));
    }

    [Theory]
    [InlineData("https://github.com/acme/app/pull/7", true)]
    [InlineData("sindri://item/itm_1", true)]
    [InlineData("http://github.com/acme/app/pull/7", false)]
    [InlineData("ftp://example.com/x", false)]
    [InlineData("sindri://other/itm_1", false)]
    [InlineData("file:///etc/passwd", false)]
    public void An_attachment_link_is_https_or_a_sindri_item(string url, bool allowed)
    {
        var error = LinearWriteLimits.Attachment(new Uri(url), "PR 7");

        if (allowed)
        {
            Assert.Null(error);
        }
        else
        {
            AssertNotSent(error);
        }
    }

    [Fact]
    public void A_link_that_is_not_absolute_is_refused()
    {
        AssertNotSent(LinearWriteLimits.Attachment(new Uri("/acme/app/pull/7", UriKind.Relative), "PR 7"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void An_attachment_title_is_not_empty(string title) => AssertNotSent(LinearWriteLimits.Attachment(new Uri("https://github.com/acme/app/pull/7"), title));

    [Fact]
    public void An_attachment_title_is_at_most_255_characters()
    {
        var url = new Uri("https://github.com/acme/app/pull/7");

        Assert.Null(LinearWriteLimits.Attachment(url, new string('t', 255)));
        AssertNotSent(LinearWriteLimits.Attachment(url, new string('t', 256)));
    }

    [Fact]
    public void Text_over_its_limit_reads_not_sent()
    {
        var error = LinearWriteLimits.Text("comment", "", required: true);

        Assert.NotNull(error);
        Assert.StartsWith("Not sent: ", error.Message, StringComparison.Ordinal);
        Assert.EndsWith(" Nothing was changed.", error.Message, StringComparison.Ordinal);
    }

    private static void AssertNotSent(Kernel.Error? error)
    {
        Assert.NotNull(error);
        Assert.Equal("SND-LINEAR-015", error.Code);
        Assert.StartsWith("Not sent: ", error.Message, StringComparison.Ordinal);
        Assert.EndsWith(" Nothing was changed.", error.Message, StringComparison.Ordinal);
    }
}
