using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;

namespace Aw.Context.Tests.Core;

[Trait("Lane", "unit")]
public sealed class ProfileAudiencePolicyTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("split-panes");

    [Fact]
    public void A_profile_repo_maps_to_the_github_entry_with_the_same_name_ignoring_case()
    {
        var key = ProfileAudiencePolicy.OwnRepo(_repo, ["acme/other", "Acme/Split-Panes"]);
        Assert.Equal("repo:Acme/Split-Panes", key?.Value);
    }

    [Fact]
    public void No_github_entry_or_two_owners_for_one_name_is_no_audience()
    {
        Assert.Null(ProfileAudiencePolicy.OwnRepo(_repo, ["acme/other"]));
        Assert.Null(ProfileAudiencePolicy.OwnRepo(_repo, ["acme/split-panes", "fork/split-panes"]));
        Assert.Null(ProfileAudiencePolicy.OwnRepo(_repo, ["split-panes", "a/b/split-panes"]));
    }

    [Fact]
    public async Task Slack_channels_are_not_in_a_sessions_audience_and_a_repo_without_a_github_entry_still_reads_its_teams()
    {
        var settings = new ContextSettings
        {
            Slack = new SlackSource { Channels = [new SlackChannel("C123", "general")] },
            LinearTeams = ["ENG"],
        };
        var scope = (await new ProfileAudiencePolicy(settings, [_repo]).For(_repo, TestContext.Current.CancellationToken)).Value;
        Assert.True(scope.Allows(AudienceKey.From("linear:ENG")));
        Assert.False(scope.Allows(AudienceKey.From("slack:C123")));
        _ = Assert.Single(scope.Allowed);
    }
}
