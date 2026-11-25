using System.Text.Json;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.MergeRun;

// #4332: merge-run runs the same readiness check as merge-check, so a reviewer's OK still only in the meta's own transcript
// (it came in during the meta's current turn) counts there too.
public sealed class MergeRunSyncTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";

    private readonly MetaWorld _world = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private MergeRunService Service() => new(_world.Readiness, _world.Repo, _world.Gates, new MergeRecorder(_world.Repo, _world.Values, _world.Gates, _world.Log, Worlds.Reservation(_world), _world.Clock, _world.Board));

    private async Task Team()
    {
        var team = JsonSerializer.Deserialize<TeamConfig>("""{"team":"t1","repo":"/work","lanes":["meta","api","ui","ideas"],"metaLane":"meta","review":{"lane":"ideas"}}""", _json)!;
        _ = await _world.Config.Save(team, CancellationToken.None);
        _ = await _world.Messages.SetRole(new MetaRole(SessionId.From(MetaWorld.MetaSessionId), "t1-meta"), CancellationToken.None);
    }

    private async Task StoredReady()
    {
        var body = $"ready #190 {Head}\nreviewed by ideas at {Head}\nGate: Aw.Meta.Tests unit 1336 passed, 0 skipped, 0 failed";
        var message = new InboxMessage(MessageId.From("m-ready"), "uds:/run/t1-api.sock", "t1-api", body, A.At(1), MessageClassifier.Classify(body));
        _ = await _world.Messages.RecordInbound(MetaWorld.Transcript, 1, [message], CancellationToken.None);
    }

    // origin/main is mainsha until the merge, newmainsha1234 after it.
    private void Script()
    {
        var merged = false;
        _ = _world.Runner
            .When("gh api graphql", QueueAnswers.Off)
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"HEAD","isDraft":false,"title":"Add x","state":"OPEN"}""".Replace("HEAD", Head, StringComparison.Ordinal))
            .When("gh pr merge 190 --merge", _ =>
            {
                merged = true;
                return new CommandResult(0, string.Empty, string.Empty, TimedOut: false);
            })
            .When("rev-parse --verify --quiet origin/main", _ => new CommandResult(0, merged ? "newmainsha1234\n" : "mainsha\n", string.Empty, TimedOut: false))
            .When("git fetch");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Merge_run_merges_on_a_reviewer_OK_from_the_meta_transcript_not_yet_synced()
    {
        await Team();
        await StoredReady();
        _world.Sessions.Add(A.Live("t1-meta", MetaWorld.MetaSessionId));
        _world.Transcripts.Write("/transcripts/" + MetaWorld.MetaSessionId + ".jsonl", MetaWorld.PeerLine("m-ok", "t1-ideas", $"OK #190 at {Head}"));
        Script();

        var result = await Service().Handle(new MergeRunRequest(190), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.True(result.Value.Merged);
        Assert.Contains("gh pr merge 190 --merge", _world.Runner.Lines, StringComparer.Ordinal);
    }
}
