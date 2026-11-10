using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Reviews;

public sealed class ReviewConnectRulesTests
{
    private static readonly GitHubAccount _missing = new(GitHubAccountState.Missing, Connection: null, Login: null);

    [Fact, Trait("Lane", "unit")]
    public void No_connection_and_a_profile_token_github_refused_shows_connect_github()
    {
        var connect = ReviewConnectRules.For(_missing, profileHasToken: true, lastCode: "AW-CONNECTORS-007");

        Assert.NotNull(connect);
        Assert.Equal("SND-REVIEW-008", connect.Code);
        Assert.Equal("Connect GitHub", connect.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_connection_and_a_working_profile_token_shows_no_action()
        => Assert.Null(ReviewConnectRules.For(_missing, profileHasToken: true, lastCode: null));

    [Fact, Trait("Lane", "unit")]
    public void No_connection_and_no_profile_token_shows_connect_github()
        => Assert.Equal("SND-REVIEW-008", ReviewConnectRules.For(_missing, profileHasToken: false, lastCode: null)?.Code);
}
