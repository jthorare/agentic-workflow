using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>
/// The GitHub event kinds of the Workflows page (#3201, spec 2026-11-12-sindri-workflows-page-design.md §4.3 and §9 E8): which feed event is which kind,
/// and for every kind the trigger payload holds exactly the kind's field list, no key missing and none extra (engine's field lists, written here as literal data).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GitHubEventKindsTests
{
    private const string Repo = "acme/app";
    private const string At = "2026-11-08T10:00:00Z";

    private static readonly string[] _envelope = ["deliveryId", "occurredAt", "connection", "event"];
    private static readonly string[] _prBase = [.. _envelope, "repo", "number", "title", "author", "url", "headSha", "baseRef"];
    private static readonly string[] _issue = [.. _envelope, "repo", "number", "title", "url", "author"];

    /// <summary>Every kind's field list (engine's, final).</summary>
    private static readonly Dictionary<string, string[]> _fields = new(StringComparer.Ordinal)
    {
        ["github.pr.opened"] = [.. _prBase, "draft"],
        ["github.pr.updated"] = [.. _prBase, "draft"],
        ["github.pr.ready_for_review"] = [.. _prBase, "draft"],
        ["github.pr.closed"] = [.. _prBase, "draft"],
        ["github.pr.merged"] = [.. _prBase, "draft"],
        ["github.pr.review_requested"] = [.. _prBase, "reviewer"],
        ["github.pr.review_submitted"] = [.. _prBase, "reviewer", "state"],
        ["github.pr.comment"] = [.. _prBase, "commenter", "body", "threadId"],
        ["github.pr.checks"] = [.. _prBase, "conclusion", "failing"],
        ["github.pr.conflict"] = [.. _prBase],
        ["github.issue.opened"] = [.. _issue],
        ["github.issue.labeled"] = [.. _issue, "label"],
        ["github.push"] = [.. _envelope, "repo", "ref", "headSha", "pusher"],
    };

    /// <summary>One feed event per kind the feed carries, by kind.</summary>
    private static readonly Dictionary<string, string> _feed = new(StringComparer.Ordinal)
    {
        ["github.pr.opened"] = GitHubFeedFixtures.PullRequest("1", Repo, 42, "opened", At),
        ["github.pr.updated"] = GitHubFeedFixtures.PullRequest("2", Repo, 42, "synchronize", At, headSha: "def456"),
        ["github.pr.ready_for_review"] = GitHubFeedFixtures.PullRequest("3", Repo, 42, "ready_for_review", At),
        ["github.pr.closed"] = GitHubFeedFixtures.PullRequest("4", Repo, 42, "closed", At),
        ["github.pr.merged"] = GitHubFeedFixtures.PullRequest("5", Repo, 42, "closed", At, merged: true),
        ["github.pr.review_requested"] = GitHubFeedFixtures.PullRequest("6", Repo, 42, "review_requested", At, reviewer: "bob"),
        ["github.pr.review_submitted"] = GitHubFeedFixtures.ReviewSubmitted("7", Repo, 42, "bob", "approved", At),
        ["github.pr.comment"] = GitHubFeedFixtures.ReviewComment("8", Repo, 42, "bob", "nit: rename", 501, inReplyTo: null, At),
        ["github.issue.opened"] = GitHubFeedFixtures.Issue("9", Repo, 7, "opened", At),
        ["github.issue.labeled"] = GitHubFeedFixtures.Issue("10", Repo, 7, "labeled", At, label: "bug"),
        ["github.push"] = GitHubFeedFixtures.Push("11", Repo, "refs/heads/main", "fed789", "ada", At),
    };

    private static readonly ConnectionName _connection = ConnectionSamples.Name("github", "work");

    /// <summary>Gets the kinds the events feed carries.</summary>
    public static TheoryData<string> FeedKinds { get; } = [.. _feed.Keys];

    [Theory]
    [MemberData(nameof(FeedKinds))]
    public void A_feed_kinds_trigger_payload_holds_exactly_its_field_list(string kind)
    {
        var polled = MapFeed(_feed[kind]);

        Assert.Equal(kind, polled.Kind.Key);
        AssertKeys(kind, polled);
    }

    [Fact]
    public void A_checks_event_holds_exactly_the_checks_field_list()
    {
        using var pull = JsonDocument.Parse(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", At));

        var polled = GitHubEventMapper.Checks(pull.RootElement, Repo, new CheckRunOutcome("red", ["build", "lint"], DateTimeOffset.Parse(At, System.Globalization.CultureInfo.InvariantCulture), RunId: 7001));

        Assert.Equal("github.pr.checks", polled.Kind.Key);
        AssertKeys("github.pr.checks", polled);
        Assert.Equal("red", polled.Fields["conclusion"]);
        Assert.Equal("[\"build\",\"lint\"]", polled.Fields["failing"]);
        Assert.Equal("abc123", polled.Fields["headSha"]);
        Assert.Equal("github:acme/app#42:checks:abc123:red:7001", polled.DeliveryKey);
    }

    [Fact]
    public void A_checks_trigger_payload_types_number_and_failing_as_the_schema_says()
    {
        using var pull = JsonDocument.Parse(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", At));
        var polled = GitHubEventMapper.Checks(pull.RootElement, Repo, new CheckRunOutcome("red", ["build", "lint"], DateTimeOffset.Parse(At, System.Globalization.CultureInfo.InvariantCulture), RunId: 7001));

        var payload = JsonNode.Parse(TriggerMatch.Trigger(_connection, polled))!.AsObject();

        Assert.Equal(JsonValueKind.Number, payload["number"]!.GetValueKind());
        Assert.Equal(42, payload["number"]!.GetValue<long>());
        Assert.Equal(["build", "lint"], payload["failing"]!.AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(JsonValueKind.String, payload["title"]!.GetValueKind());
    }

    [Theory]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    [InlineData("", JsonValueKind.Null)]
    public void A_draft_flag_is_a_boolean_in_the_trigger_payload_and_null_when_the_poll_left_it_empty(string draft, JsonValueKind expected)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["number"] = "7", ["draft"] = draft };
        var polled = new PolledEvent("github:1", EventKind.GitHubPrOpened, DateTimeOffset.UnixEpoch, fields);

        var payload = JsonNode.Parse(TriggerMatch.Trigger(_connection, polled))!.AsObject();

        Assert.Equal(expected, payload["draft"]?.GetValueKind() ?? JsonValueKind.Null);
        Assert.Equal(JsonValueKind.Number, payload["number"]!.GetValueKind());
    }

    [Fact]
    public void A_conflict_event_holds_exactly_the_shared_pull_request_fields()
    {
        using var pull = JsonDocument.Parse(GitHubFeedFixtures.OpenPull(Repo, 42, "abc123", At, mergeableState: "dirty"));

        var polled = GitHubEventMapper.Conflict(pull.RootElement, Repo, DateTimeOffset.Parse(At, System.Globalization.CultureInfo.InvariantCulture), occurrence: 1);

        Assert.Equal("github.pr.conflict", polled.Kind.Key);
        AssertKeys("github.pr.conflict", polled);
        Assert.Equal("github:acme/app#42:conflict:abc123:1", polled.DeliveryKey);
    }

    [Fact]
    public void Every_kind_of_the_list_is_covered_by_a_key_test() =>
        Assert.Equal(_fields.Keys.Order(StringComparer.Ordinal), _feed.Keys.Concat(["github.pr.checks", "github.pr.conflict"]).Order(StringComparer.Ordinal));

    [Theory]
    [InlineData("opened", false, "github.pr.opened")]
    [InlineData("synchronize", false, "github.pr.updated")]
    [InlineData("ready_for_review", false, "github.pr.ready_for_review")]
    [InlineData("review_requested", false, "github.pr.review_requested")]
    [InlineData("closed", false, "github.pr.closed")]
    [InlineData("closed", true, "github.pr.merged")]
    [InlineData("merged", true, "github.pr.merged")]
    [InlineData("reopened", false, null)]
    [InlineData("edited", false, null)]
    [InlineData("labeled", false, null)]
    public void A_PullRequestEvent_action_maps_to_its_kind(string action, bool merged, string? kind) =>
        Assert.Equal(kind, KindOf(GitHubFeedFixtures.PullRequest("1", Repo, 42, action, At, merged: merged)));

    [Fact]
    public void A_trimmed_closed_PullRequestEvent_is_a_close_and_a_trimmed_merged_one_is_a_merge()
    {
        Assert.Equal("github.pr.closed", KindOf(ProviderFixtures.GitHubPrTrimmed("1", Repo, 42, "closed", "abc123", At)));
        Assert.Equal("github.pr.merged", KindOf(ProviderFixtures.GitHubPrTrimmed("2", Repo, 42, "merged", "abc123", At)));
        Assert.Equal("github.pr.updated", KindOf(ProviderFixtures.GitHubPrTrimmed("3", Repo, 42, "synchronize", "abc123", At)));
    }

    [Theory]
    [InlineData("approved", "approved")]
    [InlineData("APPROVED", "approved")]
    [InlineData("changes_requested", "changes_requested")]
    [InlineData("commented", "commented")]
    public void A_submitted_review_maps_to_review_submitted_with_its_state(string feedState, string state)
    {
        var polled = MapFeed(GitHubFeedFixtures.ReviewSubmitted("1", Repo, 42, "bob", feedState, At));

        Assert.Equal("github.pr.review_submitted", polled.Kind.Key);
        Assert.Equal("bob", polled.Fields["reviewer"]);
        Assert.Equal(state, polled.Fields["state"]);
    }

    [Fact]
    public void A_dismissed_review_is_no_catalog_event() =>
        Assert.Null(KindOf(GitHubFeedFixtures.ReviewSubmitted("1", Repo, 42, "bob", "dismissed", At)));

    [Fact]
    public void A_reply_on_a_diff_thread_names_the_threads_first_comment_as_its_thread()
    {
        var first = MapFeed(GitHubFeedFixtures.ReviewComment("1", Repo, 42, "bob", "nit: rename", 501, inReplyTo: null, At));
        var reply = MapFeed(GitHubFeedFixtures.ReviewComment("2", Repo, 42, "ada", "done", 502, inReplyTo: 501, At));

        Assert.Equal("501", first.Fields["threadId"]);
        Assert.Equal("501", reply.Fields["threadId"]);
        Assert.Equal("ada", reply.Fields["commenter"]);
        Assert.Equal("done", reply.Fields["body"]);
    }

    [Fact]
    public void A_conversation_comment_on_a_pull_request_is_a_pr_comment_and_one_on_an_issue_is_not()
    {
        var onPull = MapFeed(GitHubFeedFixtures.IssueComment("1", Repo, 42, "bob", "LGTM", 601, onPull: true, At));

        Assert.Equal("github.pr.comment", onPull.Kind.Key);
        AssertKeys("github.pr.comment", onPull);
        Assert.Equal("601", onPull.Fields["threadId"]);
        Assert.Equal("https://github.com/acme/app/pull/42", onPull.Fields["url"]);
        Assert.Null(KindOf(GitHubFeedFixtures.IssueComment("2", Repo, 7, "bob", "same here", 602, onPull: false, At)));
    }

    [Theory]
    [InlineData("opened", "github.issue.opened")]
    [InlineData("labeled", "github.issue.labeled")]
    [InlineData("closed", null)]
    public void An_IssuesEvent_action_maps_to_its_kind(string action, string? kind) =>
        Assert.Equal(kind, KindOf(GitHubFeedFixtures.Issue("1", Repo, 7, action, At, label: "bug")));

    [Fact]
    public void A_push_carries_its_ref_head_sha_and_pusher()
    {
        var polled = MapFeed(GitHubFeedFixtures.Push("1", Repo, "refs/heads/main", "fed789", "eve", At));

        Assert.Equal("refs/heads/main", polled.Fields["ref"]);
        Assert.Equal("fed789", polled.Fields["headSha"]);
        Assert.Equal("eve", polled.Fields["pusher"]);
    }

    [Fact]
    public void An_event_no_kind_takes_maps_to_nothing() =>
        Assert.Null(KindOf(GitHubFeedFixtures.Watch("1", Repo, At)));

    [Fact]
    public void A_pull_request_payload_carries_number_draft_head_and_base()
    {
        var polled = MapFeed(GitHubFeedFixtures.PullRequest("1", Repo, 42, "opened", At, headSha: "abc123", draft: true));

        Assert.Equal("42", polled.Fields["number"]);
        Assert.Equal("true", polled.Fields["draft"]);
        Assert.Equal("abc123", polled.Fields["headSha"]);
        Assert.Equal("main", polled.Fields["baseRef"]);
        Assert.Equal(GitHubFeedFixtures.Title, polled.Fields["title"]);
        Assert.Equal(GitHubFeedFixtures.Author, polled.Fields["author"]);
        Assert.Equal("https://github.com/acme/app/pull/42", polled.Fields["url"]);
    }

    [Fact]
    public void A_trimmed_pull_request_still_has_every_key_of_its_kind_with_the_head_and_base_it_carries()
    {
        var polled = MapFeed(ProviderFixtures.GitHubPrTrimmed("1", Repo, 77, "merged", "abc123", At));

        AssertKeys("github.pr.merged", polled);
        Assert.Equal("abc123", polled.Fields["headSha"]);
        Assert.Equal("main", polled.Fields["baseRef"]);
        Assert.Equal("https://github.com/acme/app/pull/77", polled.Fields["url"]);
    }

    private static string? KindOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        return GitHubEventMapper.KindOf(document.RootElement)?.Key;
    }

    private static PolledEvent MapFeed(string json)
    {
        using var document = JsonDocument.Parse(json);
        var kind = Assert.NotNull(GitHubEventMapper.KindOf(document.RootElement));
        var polled = GitHubEventMapper.Map(document.RootElement, kind);
        Assert.NotNull(polled);
        return polled;
    }

    // why: the run's trigger JSON is what a step reads; 'kind' is the trigger block's own type ('connection_event'), not an event field
    private static void AssertKeys(string kind, PolledEvent polled)
    {
        var trigger = JsonNode.Parse(TriggerMatch.Trigger(_connection, polled))!.AsObject();
        var keys = trigger.Select(static p => p.Key).Where(static k => !string.Equals(k, "kind", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
        Assert.Equal(_fields[kind].Order(StringComparer.Ordinal), keys);
    }
}
