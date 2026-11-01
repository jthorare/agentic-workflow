using Aw.Context.Core.Extract;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Extract;

[Trait("Lane", "unit")]
public sealed class GithubRemoteTests
{
    private const string Leak = "SEC" + "RET";
    private const string Sha = "0a1b2c3d4e5f60718293a4b5c6d7e8f901234567";

    [Theory]
    [InlineData("git@github.com:acme/aw-split-panes.git")]
    [InlineData("git@github.com:acme/aw-split-panes")]
    [InlineData("ssh://git@github.com/acme/aw-split-panes.git")]
    [InlineData("https://github.com/acme/aw-split-panes")]
    [InlineData("https://github.com/acme/aw-split-panes.git/")]
    [InlineData("https://x-access-token:" + Leak + "@github.com/acme/aw-split-panes.git")]
    public void A_github_origin_names_its_owner_and_repo(string origin)
    {
        var remote = GithubRemote.Parse(origin);

        Assert.Equal(("acme", "aw-split-panes"), (remote!.Owner, remote.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("git@gitlab.com:acme/x.git")]
    [InlineData("https://github.com.evil.example/acme/x")]
    [InlineData("https://evil.example/github.com/acme/x")]
    [InlineData("http://github.com/acme/x")]
    [InlineData("https://github.com/acme/x/y")]
    [InlineData("https://github.com/../x")]
    [InlineData("/work/local/checkout")]
    public void Anything_but_a_plain_github_origin_has_no_repo(string? origin) => Assert.Null(GithubRemote.Parse(origin));

    [Fact]
    public void A_file_link_pins_a_full_sha_and_escapes_each_segment()
    {
        var link = GithubRemote.Parse("git@github.com:acme/x.git")!.Blob(Sha, "docs/a b#c.md", new FakeScrubber());

        Assert.Equal($"https://github.com/acme/x/blob/{Sha}/docs/a%20b%23c.md", link!.OriginalString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("main")]
    [InlineData("0a1b")]
    public void A_commit_that_is_not_a_full_sha_links_HEAD(string? commit) =>
        Assert.Equal("https://github.com/acme/x/blob/HEAD/a.ts", GithubRemote.Parse("git@github.com:acme/x.git")!.Blob(commit, "a.ts", egress: new FakeScrubber())!.OriginalString);

    [Theory]
    [InlineData("")]
    [InlineData("a//b")]
    [InlineData("../a.ts")]
    [InlineData("a/./b")]
    public void A_path_that_is_empty_or_climbs_has_no_link(string path) =>
        Assert.Null(GithubRemote.Parse("git@github.com:acme/x.git")!.Blob(commit: null, path, new FakeScrubber()));
}
