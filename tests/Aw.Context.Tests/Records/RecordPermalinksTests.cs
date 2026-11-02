using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Records;

/// <summary>The one reader of where a stored record opens outside Sindri (#1702): the citation and the node panel both call it.</summary>
[Trait("Lane", "unit")]
public sealed class RecordPermalinksTests
{
    private const string LinearUrl = "https://linear.app/split-panes/issue/SPL-103/toggle-split-orientation";

    private static readonly HashSet<string> _teams = new(["SPL"], StringComparer.Ordinal);

    private static StoredRecord With(string key, string? url, SourceId? source = null) => RecordSamples.Record(key, source: source) with { Url = url };

    [Fact]
    public void A_linear_issue_opens_at_its_stored_url_and_is_named_by_its_identifier()
    {
        var link = RecordPermalinks.Of(With("linear:issue/7f0c", LinearUrl), _teams, new FakeScrubber());

        Assert.Equal(new Uri(LinearUrl), link.Permalink);
        Assert.Equal(["SPL-103"], link.Aliases);
    }

    [Fact]
    public void A_github_pull_request_opens_at_its_stored_url_with_no_alias()
    {
        const string url = "https://github.com/acme/app/pull/42";
        var link = RecordPermalinks.Of(With("github:acme/app/pull/42", url, RecordSamples.Github), _teams, new FakeScrubber());

        Assert.Equal(new Uri(url), link.Permalink);
        Assert.Empty(link.Aliases);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("http://linear.app/split-panes/issue/SPL-103")]
    [InlineData("https://example.com/split-panes/issue/SPL-103")]
    public void A_missing_or_disallowed_url_gives_no_link(string? url) =>
        Assert.Null(RecordPermalinks.Of(With("linear:issue/7f0c", url), _teams, new FakeScrubber()).Permalink);

    [Theory]
    [InlineData(LinearUrl + "?t=" + FakeScrubber.Secret)]
    [InlineData(LinearUrl + "?t=FAKE%2DSECRET%2D1234")]
    [InlineData(LinearUrl + "?t=" + FakeScrubber.BlockMe)]
    public void A_url_the_scrubber_would_change_raw_or_unescaped_gives_no_link(string url) =>
        Assert.Null(RecordPermalinks.Of(With("linear:issue/7f0c", url), _teams, new FakeScrubber()).Permalink);
}
