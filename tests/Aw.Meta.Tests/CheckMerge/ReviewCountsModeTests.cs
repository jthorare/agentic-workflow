using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

// #3789: with review.mode "counts" a pull request is ready on the lane's ready line and its counts alone. The reviewer can still
// stop the review path: a standing HOLD refuses until a later OK lifts it, and a qa line under the standing OK refuses until it
// says "qa passed" at the head, wherever that OK points (#3840).
public sealed class ReviewCountsModeTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";
    private const string Old = "fedcba9876543210fedcba9876543210fedcba98";

    private readonly MetaWorld _world = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private int _next;

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages, new LaneAddress(_world.LaneFiles, _world.LaneSessions, _world.Sessions)));

    // The team file as the reader sees it: the reviewer lane under "review", with the mode when one is given.
    private async Task Team(string? mode = ReviewConfig.CountsMode)
    {
        var review = mode is null ? """{"lane":"ideas"}""" : """{"lane":"ideas","mode":"MODE"}""".Replace("MODE", mode, StringComparison.Ordinal);
        var json = """{"team":"t1","repo":"/work","lanes":["meta","api","ui","ideas"],"metaLane":"meta","review":REVIEW}""".Replace("REVIEW", review, StringComparison.Ordinal);
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

    private void Script()
    {
        var view = """{"headRefOid":"HEAD","isDraft":false,"title":"Add x","state":"OPEN"}""".Replace("HEAD", Head, StringComparison.Ordinal);
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", view)
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git fetch");
    }

    private static string ReadyBody(string sha = Head, string counts = "1336 passed, 0 skipped, 0 failed") =>
        $"ready #190 {sha}\nGate: Aw.Meta.Tests unit {counts}";

    private async Task<CheckMergeResponse> Check()
    {
        var result = await Service().Handle(new CheckMergeRequest(190), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    private static string Detail(CheckMergeResponse response, string name) => response.Checks.Single(c => c.Name == name).Detail;

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_at_the_head_with_green_counts_is_ready_with_no_OK()
    {
        await Team();
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.Empty(Failed(response));
        Assert.True(response.Ready);
        Assert.Equal("review.mode counts: no OK needed, no HOLD from t1-ideas", Detail(response, "review"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_at_an_older_head_does_not_block_in_counts_mode()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Old}");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_standing_HOLD_refuses_and_names_the_hold()
    {
        await Team();
        await Inbox("t1-api", ReadyBody());
        await Inbox("t1-ideas", "HOLD #190: the retry loop never ends");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["review"], Failed(response));
        Assert.Contains("HOLD #190: the retry loop never ends", Detail(response, "review"), StringComparison.Ordinal);
    }

    // #3840: the standing HOLD names no sha, so a push does not lift it. The lane was ready at the old head, the reviewer held, the
    // lane pushed and sent a green ready at the new head: still refused.
    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_survives_a_push_and_a_green_ready_at_the_new_head()
    {
        await Team();
        await Inbox("t1-api", ReadyBody(sha: Old));
        await Inbox("t1-ideas", "HOLD #190: the retry loop never ends");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["review"], Failed(response));
        Assert.Contains("HOLD #190: the retry loop never ends", Detail(response, "review"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_after_an_OK_at_the_head_still_refuses()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", ReadyBody());
        await Inbox("t1-ideas", "STATUS: HOLD #190 until the owner answers");
        Script();

        Assert.Equal(["review"], Failed(await Check()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_later_OK_lifts_the_HOLD()
    {
        await Team();
        await Inbox("t1-api", ReadyBody());
        await Inbox("t1-ideas", "HOLD #190");
        Script();
        Assert.False((await Check()).Ready);

        await Inbox("t1-ideas", $"OK #190 at {Head}");

        var response = await Check();
        Assert.True(response.Ready);
        Assert.Contains("said OK at", Detail(response, "review"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_for_another_pr_does_not_refuse()
    {
        await Team();
        await Inbox("t1-ideas", "HOLD #1900");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_HOLD_from_another_lane_does_not_refuse()
    {
        await Team();
        await Inbox("t1-ui", "HOLD #190");
        await Inbox("t1-api", ReadyBody());
        Script();

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Qa_not_yet_under_an_OK_at_the_head_refuses()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}\nqa not yet");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["review"], Failed(response));
        Assert.Contains("qa not yet", Detail(response, "review"), StringComparison.Ordinal);
    }

    // #3840: the qa block stands per pull request like a HOLD. A push plus a green ready at the new head does not lift it.
    [Fact, Trait("Lane", "unit")]
    public async Task Qa_not_yet_under_an_OK_at_an_older_head_still_refuses()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Old}\nqa not yet");
        await Inbox("t1-api", ReadyBody());
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["review"], Failed(response));
        Assert.Contains("qa not yet", Detail(response, "review"), StringComparison.Ordinal);
    }

    // A qa pass at the old head says nothing about the new one; a new OK with "qa passed <head>" clears it.
    [Fact, Trait("Lane", "unit")]
    public async Task Qa_passed_at_an_older_head_refuses_until_qa_passes_at_the_head()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Old}\nqa passed {Old}");
        await Inbox("t1-api", ReadyBody());
        Script();

        var refused = await Check();
        Assert.False(refused.Ready);
        Assert.Equal(["review"], Failed(refused));
        Assert.Contains($"qa passed at {Old}", Detail(refused, "review"), StringComparison.Ordinal);

        await Inbox("t1-ideas", $"OK #190 at {Head}\nqa passed {Head}");

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_ready_message_refuses()
    {
        await Team();
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Contains("ready", Failed(response), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_at_another_sha_refuses()
    {
        await Team();
        await Inbox("t1-api", ReadyBody(sha: Old));
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["ready"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_with_no_count_line_refuses()
    {
        await Team();
        await Inbox("t1-api", $"ready #190 {Head}\nbuild and tests green");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ready_with_a_failed_count_refuses()
    {
        await Team();
        await Inbox("t1-api", ReadyBody(counts: "1336 passed, 0 skipped, 0 failed\nAw.Meta.Tests integration 40 passed, 0 skipped, 2 failed"));
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
    }

    // #3840: the build-only marker is a count line of its own shape, alone on its line (trimmed, any case).
    private const string BuildOnly = "  0 passed, 0 skipped, 0 failed (build only)  ";

    // The old prose form: "(build only)" next to other words is not the marker.
    private const string BuildOnlyProse = "build -warnaserror 0 warnings, 0 errors (build only)";

    [Theory, Trait("Lane", "unit")]
    [InlineData(BuildOnly)]
    [InlineData("0 Passed, 0 Skipped, 0 Failed (Build Only)\r")]
    public async Task A_build_only_line_with_no_other_count_line_is_ready_in_counts_mode(string line)
    {
        await Team();
        await Inbox("t1-api", $"ready #190 {Head}\n{line}");
        Script();

        var response = await Check();

        Assert.True(response.Ready);
        Assert.Equal("build only, no test counts", Detail(response, "counts"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(BuildOnlyProse)]
    [InlineData("Gate: built only, so no tests ran (build only)")]
    [InlineData("Aw.Meta.Tests unit 0 passed, 0 skipped, 0 failed (build only)")]
    [InlineData("0 passed, 0 skipped, 0 failed (build only), tests next")]
    public async Task Build_only_outside_its_own_line_does_not_count_in_counts_mode(string line)
    {
        await Team();
        await Inbox("t1-api", $"ready #190 {Head}\n{line}");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
    }

    // A filtered run that matched no test counts zero; without the marker that proves nothing.
    [Theory, Trait("Lane", "unit")]
    [InlineData("0 passed, 0 skipped, 0 failed")]
    [InlineData("0 passed, 12 skipped, 0 failed")]
    public async Task Counts_that_total_zero_passed_refuse_in_counts_mode(string counts)
    {
        await Team();
        await Inbox("t1-api", ReadyBody(counts: counts));
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
        Assert.Contains("0 passed", Detail(response, "counts"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_zero_count_line_next_to_a_green_one_is_ready_in_counts_mode()
    {
        await Team();
        await Inbox("t1-api", ReadyBody(counts: "0 passed, 0 skipped, 0 failed\nAw.Meta.Tests integration 40 passed, 0 skipped, 0 failed"));
        Script();

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_only_line_next_to_a_failed_count_refuses()
    {
        await Team();
        await Inbox("t1-api", $"ready #190 {Head}\n{BuildOnly}\nAw.Meta.Tests unit 40 passed, 0 skipped, 1 failed");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_only_note_is_not_a_count_line_in_ok_mode()
    {
        await Team(ReviewConfig.OkMode);
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", $"ready #190 {Head}\n{BuildOnlyProse}");
        Script();

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["counts"], Failed(response));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData(ReviewConfig.OkMode)]
    public async Task The_default_mode_still_needs_the_reviewers_OK(string? mode)
    {
        await Team(mode);
        await Inbox("t1-api", ReadyBody());
        Script();

        var refused = await Check();
        Assert.False(refused.Ready);
        Assert.Equal(["review"], Failed(refused));
        Assert.Contains("no standing \"OK #190 at <sha>\" from t1-ideas", Detail(refused, "review"), StringComparison.Ordinal);

        await Inbox("t1-ideas", $"OK #190 at {Head}");

        Assert.True((await Check()).Ready);
    }
}
