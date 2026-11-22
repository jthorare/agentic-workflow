using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>The scan's pure rules (first-run spec §3.3): skipped folder names, the home-folder bound and the origin cut to host and slug.</summary>
public sealed class RepoScanRulesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(".git", true)]
    [InlineData(".cache", true)]
    [InlineData("node_modules", true)]
    [InlineData("bin", true)]
    [InlineData("obj", true)]
    [InlineData("target", true)]
    [InlineData("repos", false)]
    [InlineData("binder", false)]
    [InlineData("Target", false)]
    public void The_walk_skips_dot_folders_and_build_folders(string name, bool skipped) => Assert.Equal(skipped, RepoScanRules.Skips(name));

    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/joi", true)]
    [InlineData("/home/joi/repos", true)]
    [InlineData("/home/joi/", true)]
    [InlineData("/home/joiner", false)]
    [InlineData("/home", false)]
    [InlineData("/", false)]
    public void A_root_must_be_the_home_folder_or_under_it(string path, bool inside) => Assert.Equal(inside, RepoScanRules.InsideHome(path, "/home/joi"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://github.com/acme/app.git", "github.com", "acme/app")]
    [InlineData("https://joi:" + "opensesame@github.com/acme/app.git", "github.com", "acme/app")]
    [InlineData("https://bot@github.com/acme/app", "github.com", "acme/app")]
    [InlineData("git@github.com:acme/app.git", "github.com", "acme/app")]
    [InlineData("ssh://git@github.com/acme/app.git", "github.com", "acme/app")]
    [InlineData("ssh://git@github.com:22/acme/app.git", "github.com", "acme/app")]
    [InlineData("https://GitHub.com/Acme/App.git/", "github.com", "Acme/App")]
    [InlineData("https://gitlab.example.com/acme/app.git", "gitlab.example.com", "acme/app")]
    public void The_origin_is_its_host_and_slug_without_userinfo(string url, string host, string slug) => Assert.Equal((host, slug), RepoScanRules.Origin(url));

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/srv/git/app.git")]
    [InlineData("file:///srv/git/app.git")]
    [InlineData("https://github.com/acme")]
    [InlineData("https://github.com/acme/group/app.git")]
    [InlineData("not a url")]
    public void An_origin_without_a_host_and_two_part_path_has_none(string? url) => Assert.Null(RepoScanRules.Origin(url));
}
