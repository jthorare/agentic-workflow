using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>#3534: the starter templates read CI only for a github.com origin; any other remote gives no source.</summary>
[Lane(TestLane.Unit)]
public sealed class GitHubCiSourceLookupTests
{
    [Theory]
    [InlineData("https://github.com/acme/app.git", "acme", "app")]
    [InlineData("https://github.com/acme/app", "acme", "app")]
    [InlineData("https://github.com/acme/app/", "acme", "app")]
    [InlineData("git@github.com:acme/app.git", "acme", "app")]
    [InlineData("git@github.com:acme/app", "acme", "app")]
    [InlineData("ssh://git@github.com/acme/app.git", "acme", "app")]
    public void A_github_origin_gives_its_owner_and_repo(string url, string owner, string repo)
        => Assert.Equal((owner, repo), GitHubCiSourceLookup.ParseRemote(url));

    [Theory]
    [InlineData("https://gitlab.com/acme/app.git")]
    [InlineData("git@bitbucket.org:acme/app.git")]
    [InlineData("https://github.com.evil.example/acme/app.git")]
    [InlineData("https://github.com/acme")]
    [InlineData("/home/me/repos/app")]
    [InlineData("")]
    public void Any_other_host_or_shape_gives_no_source(string url)
        => Assert.Null(GitHubCiSourceLookup.ParseRemote(url));
}
