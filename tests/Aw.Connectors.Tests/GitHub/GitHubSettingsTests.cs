using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Tests.GitHub;

public sealed class GitHubSettingsTests
{
    private const string Pointer = "env:GITHUB_READ_KEY";

    private static Result<GitHubSettings> Make(IReadOnlyList<string> repos, string? token, string? host, bool allowPrivateHost, int? commentCap)
        => GitHubSettings.Create(repos, token, host, allowPrivateHost, commentCap);

    [Fact, Trait("Lane", "unit")]
    public void Valid_values_make_settings_with_the_default_comment_cap()
    {
        var settings = Make(["acme/widgets", "ACME/widgets", "acme/api"], token: Pointer, host: null, allowPrivateHost: false, commentCap: null).Value;

        Assert.Equal(["acme/widgets", "acme/api"], settings.Repos);
        Assert.Equal(GitHubSettings.DefaultCommentCap, settings.CommentCap);
        Assert.Equal(Pointer, settings.Token);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("widgets")]
    [InlineData("a/b/c")]
    public void A_repo_that_is_not_owner_slash_name_is_refused(string repo)
        => Assert.Equal(ConnectorsErrors.Invalid.Code, Make([repo], token: null, host: null, allowPrivateHost: false, commentCap: null).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void No_repos_is_refused() => Assert.True(Make([], token: null, host: null, allowPrivateHost: false, commentCap: null).IsError);

    [Fact, Trait("Lane", "unit")]
    public void A_value_that_is_not_a_pointer_is_refused_and_never_repeated()
    {
        const string Planted = "planted-value-42";

        var error = Make(["acme/widgets"], token: Planted, host: null, allowPrivateHost: false, commentCap: null).Error;

        Assert.DoesNotContain(Planted, error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("https://ghe.example.com")]
    [InlineData("ghe.example.com/api")]
    [InlineData("user@ghe.example.com")]
    public void A_host_with_a_scheme_path_or_user_is_refused(string host)
        => Assert.True(Make(["acme/widgets"], token: null, host: host, allowPrivateHost: false, commentCap: null).IsError);

    [Theory, Trait("Lane", "unit")]
    [InlineData(-1)]
    [InlineData(1001)]
    public void A_comment_cap_out_of_range_is_refused(int cap)
        => Assert.True(Make(["acme/widgets"], token: null, host: null, allowPrivateHost: false, commentCap: cap).IsError);

    [Fact, Trait("Lane", "unit")]
    public void ToString_withholds_the_pointer()
        => Assert.DoesNotContain(Pointer, Make(["acme/widgets"], token: Pointer, host: null, allowPrivateHost: false, commentCap: null).Value.ToString(), StringComparison.Ordinal);
}
