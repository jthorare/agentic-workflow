using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

// #2610: a pull request with no meta gate run is ready when the team's reviewer lane sent "OK #<pr> at <head>" and the
// lane's latest ready message names the head and carries the lane's own "N passed, M skipped, 0 failed" counts.
public sealed class ReviewMergeTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";
    private const string Old = "fedcba9876543210fedcba9876543210fedcba98";

    private readonly MetaWorld _world = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private int _next;

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages));

    // The team file as the reader sees it: the reviewer lane under "review", an optional key.
    private async Task Team(bool withReview = true)
    {
        var review = withReview ? ""","review":{"lane":"ideas"}""" : string.Empty;
        var json = """{"team":"t1","repo":"/work","lanes":["meta","api","ui","ideas"],"metaLane":"meta"REVIEW}""".Replace("REVIEW", review, StringComparison.Ordinal);
        var team = JsonSerializer.Deserialize<TeamConfig>(json, _json)!;
        _ = await _world.Config.Save(team, CancellationToken.None);
        _ = await _world.Messages.SetRole(new MetaRole(SessionId.From(MetaWorld.MetaSessionId), "t1-meta"), CancellationToken.None);
    }

    private async Task Inbox(string session, string body)
    {
        var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"m-{++_next}"));
        var message = new InboxMessage(id, "uds:/run/" + session + ".sock", session, body, A.At(_next), MessageClassifier.Classify(body));
        _ = await _world.Messages.RecordInbound(MetaWorld.Transcript, _next, [message], CancellationToken.None);
    }

    private void Script(int pr = 190, string head = Head)
    {
        var view = """{"headRefOid":"HEAD","isDraft":false,"title":"Add x","state":"OPEN"}""".Replace("HEAD", head, StringComparison.Ordinal);
        _ = _world.Runner
            .When(string.Create(CultureInfo.InvariantCulture, $"gh pr view {pr} --json headRefOid,isDraft,title,state"), view)
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git fetch");
    }

    private static string ReadyBody(string sha = Head, string counts = "1336 passed, 0 skipped, 0 failed") =>
        $"ready #190 {sha}\nreviewed by ideas at {sha}\nGate: Aw.Meta.Tests unit {counts}";

    private async Task<CheckMergeResponse> Check()
    {
        var result = await Service().Handle(new CheckMergeRequest(190), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_reviewer_OK_at_the_head_and_the_lanes_counts_are_ready_with_no_gate()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}\nOK #191 at abcdef1234567");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.DoesNotContain("gate", Failed(response), StringComparer.Ordinal);
        Assert.True(response.Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_at_an_older_head_is_not_ready()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Old}");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.False((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_message_with_a_failed_count_is_not_ready()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", ReadyBody(counts: "1333 passed, 0 skipped, 3 failed"));
        Script();

        Assert.False((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_review_lane_a_never_gated_pr_is_refused_as_today()
    {
        await Team(withReview: false);
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Contains("gate", Failed(response), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_from_another_lane_does_not_count()
    {
        await Team();
        await Inbox("t1-ui", $"OK #190 at {Head}");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.False((await Check()).Ready);
    }

    // Review fix on #2614: the reviewer voids an OK at an unchanged head (main moved under it) with "HOLD #<pr>"; only a later OK counts again.
    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_after_the_OK_is_not_ready_until_a_new_OK()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", ReadyBody());
        await Inbox("t1-ideas", "HOLD #190: main changed AgentLaunchEnded under it");
        Script();

        var held = await Check();
        Assert.False(held.Ready);
        Assert.Contains("review", Failed(held), StringComparer.Ordinal);

        await Inbox("t1-ideas", $"OK #190 at {Head}");

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_for_another_pr_keeps_this_OK()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-ideas", "HOLD #1900");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }

    // A later reviewer message that names no OK or HOLD for the pull request keeps the standing OK.
    [Fact, Trait("Lane", "unit")]
    public async Task A_later_reviewer_message_without_an_OK_keeps_the_OK()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-ideas", "OK #191 at abcdef1234567\nlooking at #192 next");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }

    // Meta's UI rule: the line after the OK is the qa line. "qa not yet" refuses; "qa passed <sha>" must name the head.
    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_with_qa_not_yet_is_not_ready()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}\nqa not yet");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();
        Assert.False(response.Ready);
        Assert.Contains("review", Failed(response), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_with_qa_passed_at_an_older_head_is_not_ready()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}\nqa passed {Old}");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.False((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_with_qa_passed_at_the_head_is_ready()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}\nqa passed {Head[..10]}\nOK #191 at abcdef1234567\nqa not yet");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }
}
