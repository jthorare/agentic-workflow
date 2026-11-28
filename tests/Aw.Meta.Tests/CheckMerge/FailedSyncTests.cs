using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

// #4332: merge-check syncs the meta's own transcript first. A sync that fails is advisory: every check still runs on the
// stored messages, and a passing "inbox-sync" row quotes the error.
public sealed class FailedSyncTests
{
    private const string Head = "0123456789abcdef0123456789abcdef01234567";

    private readonly MetaWorld _world = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private int _next;

    // The resolver finds the meta's transcript in _world.Transcripts, but the sync reads through an empty reader, so the read fails.
    private MergeReadiness Readiness(bool withSync)
    {
        var resolver = new MetaTranscriptResolver(_world.Messages, _world.Sessions, new InMemoryTranscriptLocator(_world.Transcripts));
        var broken = new InboxSync(new InMemoryTranscriptReader(), _world.Messages, _world.Owner, _world.Clock, resolver);
        return new MergeReadiness(_world.Config, _world.Gates, _world.Repo, _world.LogFiles, _world.Messages, new LaneAddress(_world.LaneFiles, _world.LaneSessions, _world.Sessions), withSync ? broken : null);
    }

    private async Task Team()
    {
        var team = JsonSerializer.Deserialize<TeamConfig>("""{"team":"t1","repo":"/work","lanes":["meta","api","ui","ideas"],"metaLane":"meta","review":{"lane":"ideas"}}""", _json)!;
        _ = await _world.Config.Save(team, CancellationToken.None);
        _ = await _world.Messages.SetRole(new MetaRole(SessionId.From(MetaWorld.MetaSessionId), "t1-meta"), CancellationToken.None);
    }

    private async Task Inbox(string session, string body)
    {
        var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"m-{++_next}"));
        var message = new InboxMessage(id, "uds:/run/" + session + ".sock", session, body, A.At(_next), MessageClassifier.Classify(body));
        _ = await _world.Messages.RecordInbound(MetaWorld.Transcript, _next, [message], CancellationToken.None);
    }

    private void Script() =>
        _ = _world.Runner
            .When("gh pr view 190 --json headRefOid,isDraft,title,state", """{"headRefOid":"HEAD","isDraft":false,"title":"Add x","state":"OPEN"}""".Replace("HEAD", Head, StringComparison.Ordinal))
            .When("rev-parse --verify --quiet origin/main", "mainsha\n")
            .When("git fetch");

    private async Task<CheckMergeResponse> Check(bool withSync)
    {
        var result = await new CheckMergeService(Readiness(withSync)).Handle(new CheckMergeRequest(190), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_sync_still_prints_every_check_and_adds_a_passing_inbox_sync_row()
    {
        await Team();
        await Inbox("t1-ideas", $"OK #190 at {Head}");
        await Inbox("t1-api", $"ready #190 {Head}\nreviewed by ideas at {Head}\nGate: Aw.Meta.Tests unit 1336 passed, 0 skipped, 0 failed");
        _world.Sessions.Add(A.Live("t1-meta", MetaWorld.MetaSessionId));
        _world.Transcripts.Write("/transcripts/" + MetaWorld.MetaSessionId + ".jsonl", string.Empty);
        Script();

        var stored = await Check(withSync: false);
        var response = await Check(withSync: true);

        var note = Assert.Single(response.Checks, c => c.Name == "inbox-sync");
        Assert.True(note.Ok);
        Assert.Contains("no transcript /transcripts/" + MetaWorld.MetaSessionId + ".jsonl", note.Detail, StringComparison.Ordinal);
        Assert.Equal(stored.Checks, response.Checks.Where(c => c.Name != "inbox-sync"));
        Assert.True(response.Ready);
    }
}
