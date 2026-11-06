using System.Text.Json;
using Aw.Connectors.Infrastructure.GitHub;

namespace Aw.Connectors.Tests.GitHub;

/// <summary>PR review page spec §5.6: the single-pull-request reply's reviewers and head, and the review list reduced to each reviewer's latest review.</summary>
public sealed class GitHubWireReviewTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement Review(string login, string state, string? commit, string submittedAt) =>
        Json($$"""{"user":{"login":"{{login}}"},"state":"{{state}}","commit_id":{{(commit is null ? "null" : $"\"{commit}\"")}},"submitted_at":"{{submittedAt}}"}""");

    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_with_two_requested_reviewers_keeps_both_logins_and_the_head()
    {
        var detail = GitHubWire.ReadPull(Json("""{"merged":false,"requested_reviewers":[{"login":"bob"},{"login":"cy"}],"head":{"sha":"abc123"}}"""), []);

        Assert.Equal(["bob", "cy"], detail.RequestedReviewers);
        Assert.Equal("abc123", detail.HeadSha);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pull_request_with_no_requested_reviewers_and_no_head_reads_as_none()
    {
        var detail = GitHubWire.ReadPull(Json("""{"merged":false}"""), []);

        Assert.Empty(detail.RequestedReviewers ?? []);
        Assert.Null(detail.HeadSha);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_request_to_a_team_alone_gives_no_reviewer_logins()
    {
        var detail = GitHubWire.ReadPull(Json("""{"requested_reviewers":[],"requested_teams":[{"slug":"core","name":"Core"}],"head":{"sha":"abc123"}}"""), []);

        Assert.Empty(detail.RequestedReviewers ?? []);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_logins_latest_review_by_submitted_at_wins_whatever_the_order()
    {
        var latest = GitHubWire.LatestReviews(
        [
            Review("bob", "COMMENTED", "c3", "2026-11-02T10:00:00Z"),
            Review("bob", "APPROVED", "c1", "2026-11-01T10:00:00Z"),
            Review("ada", "APPROVED", "c2", "2026-11-01T11:00:00Z"),
        ]);

        Assert.Equal([("ada", "c2"), ("bob", "c3")], latest);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_review_is_skipped_so_the_one_before_it_counts()
    {
        var latest = GitHubWire.LatestReviews(
        [
            Review("bob", "APPROVED", "c1", "2026-11-01T10:00:00Z"),
            Review("bob", "PENDING", "c9", "2026-11-03T10:00:00Z"),
            Review("ada", "PENDING", "c8", "2026-11-03T10:00:00Z"),
        ]);

        Assert.Equal([("bob", "c1")], latest);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_review_without_a_login_or_a_commit_is_skipped()
    {
        var latest = GitHubWire.LatestReviews(
        [
            Json("""{"user":null,"state":"APPROVED","commit_id":"c1","submitted_at":"2026-11-01T10:00:00Z"}"""),
            Review("bob", "APPROVED", commit: null, "2026-11-01T10:00:00Z"),
        ]);

        Assert.Empty(latest);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ahead_by_is_read_from_a_compare_reply_and_is_null_without_it()
    {
        Assert.Equal(2, GitHubWire.AheadBy(Json("""{"status":"ahead","ahead_by":2}""")));
        Assert.Null(GitHubWire.AheadBy(Json("""{"status":"diverged"}""")));
    }
}
