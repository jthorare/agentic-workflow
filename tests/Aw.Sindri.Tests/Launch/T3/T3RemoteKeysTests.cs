using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>The repository key matches the one T3 Code computes, so a Sindri repo finds the T3 project of the same repository.</summary>
[Lane(TestLane.Unit)]
public sealed class T3RemoteKeysTests
{
    [Theory]
    [InlineData("git@github.com:jthorare/agentic-workflow.git", "github.com/jthorare/agentic-workflow")]
    [InlineData("https://github.com/JThorare/Agentic-Workflow.git/", "github.com/jthorare/agentic-workflow")]
    [InlineData("ssh://git@github.com:22/jthorare/agentic-workflow", "github.com/jthorare/agentic-workflow")]
    [InlineData("git@gitlab.example.com:group/sub/repo.git", "gitlab.example.com/group/sub/repo")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/repo", "dev.azure.com/org/project/_git/repo")]
    [InlineData("/srv/git/repo.git", "/srv/git/repo")]
    public void A_remote_url_is_normalized_like_T3_does(string url, string key) => Expect.Equal(key, T3RemoteKeys.Normalize(url));

    [Fact]
    public void Upstream_is_the_primary_remote_then_origin_then_the_first_by_name()
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal) { ["origin"] = "git@github.com:me/fork.git", ["upstream"] = "git@github.com:them/repo.git", ["a"] = "git@github.com:a/a.git" };
        var origin = new Dictionary<string, string>(StringComparer.Ordinal) { ["origin"] = "git@github.com:me/fork.git", ["a"] = "git@github.com:a/a.git" };
        var others = new Dictionary<string, string>(StringComparer.Ordinal) { ["zed"] = "git@github.com:z/z.git", ["bee"] = "git@github.com:b/b.git" };

        Expect.Equal("github.com/them/repo", T3RemoteKeys.Primary(all));
        Expect.Equal("github.com/me/fork", T3RemoteKeys.Primary(origin));
        Expect.Equal("github.com/b/b", T3RemoteKeys.Primary(others));
        Expect.True(T3RemoteKeys.Primary(new Dictionary<string, string>(StringComparer.Ordinal)) is null);
    }
}
