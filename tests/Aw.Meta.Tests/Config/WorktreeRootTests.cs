using Aw.Meta.Core;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Tests.Config;

public sealed class WorktreeRootTests
{
    private const string Repo = "/work/repo";
    private const string Home = "/home/u";

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "/home/u/repos/aw-wt/repo")]
    [InlineData("", "/home/u/repos/aw-wt/repo")]
    [InlineData("/data/trees", "/data/trees")]
    [InlineData("~", "/home/u")]
    [InlineData("~/x", "/home/u/x")]
    [InlineData("../trees", "/work/trees")]
    [InlineData("/data//a/./b/../c/", "/data/a/c")]
    [InlineData("/work/repo-wt", "/work/repo-wt")]
    public void The_root_is_resolved_the_way_the_skills_do(string? configured, string expected)
    {
        var result = WorktreeRoot.Resolve(configured, Repo, Home);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/repo")]
    [InlineData("/work/repo/")]
    [InlineData("/work/repo/trees")]
    [InlineData("trees")]
    [InlineData("../repo/x")]
    public void A_root_equal_to_the_repo_or_inside_it_is_refused(string configured)
    {
        var result = WorktreeRoot.Resolve(configured, Repo, Home);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.WorktreeRootInRepo.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("~/x")]
    public void A_missing_home_is_an_error_when_the_root_needs_it(string? configured)
    {
        var result = WorktreeRoot.Resolve(configured, Repo, string.Empty);

        Assert.Equal(MetaErrors.WorktreeHomeUnknown.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_home_does_not_matter_for_an_absolute_root() =>
        Assert.Equal("/data/trees", WorktreeRoot.Resolve("/data/trees", Repo, string.Empty).Value);
}
