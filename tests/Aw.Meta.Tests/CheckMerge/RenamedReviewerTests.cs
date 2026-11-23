using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

// #4103: lane-recycle's /clear leaves the digest prompt as the session name, so the reviewer lane's OK lines arrive under a name
// that is not "team-lane". The lane's running session still sends from the same socket, and that socket is what counts.
public sealed class RenamedReviewerTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";
    private const string DigestName = "Read and continue from /home/u/.agentic-workflow/digests/t1-ideas-2026-11-23.md";
    private const string IdeasSocket = "uds:/run/user/1000/cc-socks/4242.sock";
    private const string ApiSocket = "uds:/run/user/1000/cc-socks/5151.sock";

    private readonly MetaWorld _world = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private int _next;

    private CheckMergeService Service() => new(new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages, new LaneAddress(_world.LaneFiles, _world.LaneSessions, _world.Sessions)));

    private async Task Team()
    {
        var team = JsonSerializer.Deserialize<TeamConfig>("""{"team":"t1","repo":"/work","lanes":["meta","api","ui","ideas"],"metaLane":"meta","review":{"lane":"ideas"}}""", _json)!;
        _ = await _world.Config.Save(team, CancellationToken.None);
        _ = await _world.Messages.SetRole(new MetaRole(SessionId.From(MetaWorld.MetaSessionId), "t1-meta"), CancellationToken.None);
    }

    // A lane's Claude runs in its tmux session "t1-<lane>" under a name that is not "t1-<lane>" any more.
    private void Running(string lane, string id, int pid, string socket)
    {
        _world.LaneSessions.Set("t1-" + lane, id);
        _world.Sessions.Add(new LiveSession(SessionId.From(id), DigestName, pid, "idle", "/work") { Address = socket });
    }

    private async Task Inbox(string from, string name, string body)
    {
        var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"m-{++_next}"));
        var message = new InboxMessage(id, from, name, body, A.At(_next), MessageClassifier.Classify(body));
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

    private async Task<CheckMergeResponse> Check()
    {
        await Inbox("uds:/run/t1-api.sock", "t1-api", $"ready #190 {Head}\nGate: Aw.Meta.Tests unit 1336 passed, 0 skipped, 0 failed");
        Script();
        var result = await Service().Handle(new CheckMergeRequest(190), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string[] Failed(CheckMergeResponse response) => [.. response.Checks.Where(c => !c.Ok).Select(c => c.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_from_the_review_lane_counts_after_its_session_was_renamed()
    {
        await Team();
        Running("ideas", "s-ideas", 4242, IdeasSocket);
        await Inbox(IdeasSocket, DigestName, $"OK #190 at {Head}");

        var response = await Check();

        Assert.Empty(Failed(response));
        Assert.True(response.Ready);
    }

    // The lane-sessions record is the fallback when the lane's tmux session shows no Claude id (the recycle records the new id there).
    [Fact, Trait("Lane", "unit")]
    public async Task The_recorded_session_finds_the_renamed_reviewer_when_its_pane_shows_none()
    {
        await Team();
        _world.LaneFiles.Put(new LaneRecord("ideas", SessionId.From("s-ideas"), Agent: null));
        _world.Sessions.Add(new LiveSession(SessionId.From("s-ideas"), DigestName, 4242, "idle", "/work") { Address = IdeasSocket });
        await Inbox(IdeasSocket, DigestName, $"OK #190 at {Head}");

        Assert.True((await Check()).Ready);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_OK_from_another_lanes_session_does_not_count_under_any_name()
    {
        await Team();
        Running("ideas", "s-ideas", 4242, IdeasSocket);
        Running("api", "s-api", 5151, ApiSocket);
        await Inbox(ApiSocket, DigestName, $"OK #190 at {Head}");

        var response = await Check();

        Assert.False(response.Ready);
        Assert.Equal(["review"], Failed(response));
    }
}
