using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Testing;

/// <summary>Every port as its in-memory fake, the shared coordination services over them and a clock: what a slice test needs. A new instance is a fresh machine.</summary>
public sealed class MetaWorld
{
    /// <summary>The meta session's transcript path.</summary>
    public const string Transcript = "/transcripts/meta.jsonl";

    /// <summary>The registered meta session id.</summary>
    public const string MetaSessionId = "s-meta";

    /// <summary>The clock, which starts an hour after <see cref="A.T0"/>.</summary>
    public ManualClock Clock { get; } = new(A.At(60));

    /// <summary>Messages, offsets and the role.</summary>
    public InMemoryMessageStore Messages { get; } = new();

    /// <summary>The lane map.</summary>
    public InMemoryLaneStore Lanes { get; } = new();

    /// <summary>Owner questions.</summary>
    public InMemoryOwnerStore Owner { get; } = new();

    /// <summary>Gates and merges.</summary>
    public InMemoryGateStore Gates { get; } = new();

    /// <summary>Small values.</summary>
    public InMemoryKeyValueStore Values { get; } = new();

    /// <summary>The team config.</summary>
    public InMemoryMetaConfig Config { get; } = new();

    /// <summary>Live sessions.</summary>
    public InMemorySessionDirectory Sessions { get; } = new();

    /// <summary>The lane records the launcher and the session-id hook left.</summary>
    public InMemoryLaneFiles LaneFiles { get; } = new();

    /// <summary>The lanes' tmux sessions.</summary>
    public InMemoryLanePane Panes { get; } = new();

    /// <summary>The running log.</summary>
    public InMemoryRunningLog Log { get; } = new();

    /// <summary>Transcript files.</summary>
    public InMemoryTranscriptReader Transcripts { get; } = new();

    /// <summary>Claude Code's session transcripts.</summary>
    public InMemoryClaudeTranscripts ClaudeTranscripts { get; } = new();

    /// <summary>The session each Claude lane runs now (Claude Code's sessions directory).</summary>
    public InMemoryLaneSessions LaneSessions { get; } = new();

    /// <summary>The digest files lanes wrote.</summary>
    public InMemoryDigestFiles Digests { get; } = new();

    /// <summary>External programs (git, gh and the gate's steps), scripted per test.</summary>
    public InMemoryCommandRunner Runner { get; } = new();

    /// <summary>Gate step logs.</summary>
    public InMemoryLogFiles LogFiles { get; } = new();

    /// <summary>Memory, disk and heavy-job locks.</summary>
    public InMemoryHostResources Resources { get; } = new();

    /// <summary>The team's kept heavy reservation (heavy-reserve).</summary>
    public InMemoryHeavyReserve Heavy { get; } = new();

    /// <summary>Processes.</summary>
    public InMemoryProcessTable Processes { get; } = new();

    /// <summary>Where processes are (working directories, open files).</summary>
    public InMemoryProcessLocations Locations { get; } = new();

    /// <summary>When worktree directories last changed.</summary>
    public InMemoryWorktreeFiles WorktreeFiles { get; } = new();

    /// <summary>What the bugfix runs recorded.</summary>
    public InMemoryBugfixRecords BugfixRecords { get; } = new();

    /// <summary>The delay between polls; waiting moves <see cref="Clock"/>.</summary>
    public InMemoryDelay Delay { get; }

    /// <summary>Where files live and which programs run: the gate directory is /state/meta/gates, the heavy wrapper /bin/heavy.</summary>
    public MetaPaths Paths { get; } = new("/state/meta", "/state/meta/gates", "/bin/heavy", "gh");

    /// <summary>The worktree inspector over <see cref="Runner"/> and <see cref="Locations"/>.</summary>
    public WorktreeInspector Inspector => new(Repo, Locations, Paths, Values, WorktreeFiles, BugfixRecords);

    /// <summary>The worktree sweeper over the fakes.</summary>
    public WorktreeSweeper Sweeper => new(Inspector, Repo, Log, Values, Clock);

    /// <summary>The git and gh calls over <see cref="Runner"/>.</summary>
    public RepoCommands Repo => new(Runner, Paths);

    /// <summary>The gate worktrees over <see cref="Runner"/>.</summary>
    public GateTree Trees => new(Repo);

    /// <summary>The messages to and from the non-Claude lanes, over <see cref="Messages"/>.</summary>
    public InMemoryLaneMail LaneMail { get; }

    /// <summary>A world with all its fakes.</summary>
    public MetaWorld()
    {
        Delay = new InMemoryDelay(Clock);
        LaneMail = new InMemoryLaneMail(Messages);
    }

    /// <summary>The lane hook heartbeats.</summary>
    public LaneBeats Beats => new(Values, Delay);

    /// <summary>The lane stop gate's loop guard.</summary>
    public LaneStopGuard LaneGuard => new(Values, Log);

    /// <summary>A lane-health reader over empty fakes: a team with no non-Claude lane.</summary>
    /// <returns>The reader.</returns>
    public static LaneHealthReader NoNonClaudeLanes()
    {
        var values = new InMemoryKeyValueStore();
        return new LaneHealthReader(new InMemoryLaneFiles(), new LaneBeats(values, new InMemoryDelay(new ManualClock(A.T0))), new InMemoryLaneMail(new InMemoryMessageStore()), new LaneStopGuard(values, new InMemoryRunningLog()));
    }

    /// <summary>The lanes' context sizes.</summary>
    public LaneContextReader Context => new(LaneFiles, LaneSessions, ClaudeTranscripts);

    /// <summary>The non-Claude lanes' standing.</summary>
    public LaneHealthReader LaneHealth => new(LaneFiles, Beats, LaneMail, LaneGuard);

    /// <summary>The inbox sync.</summary>
    public InboxSync Sync => new(Transcripts, Messages, Owner, Clock);

    /// <summary>The lane refresher.</summary>
    public LaneRefresher Refresher => new(Sessions, Messages, Lanes);

    /// <summary>The obligation reader.</summary>
    public ObligationReader Reader => new(Messages, Lanes, Owner, Gates, Values);

    /// <summary>The meta-session check.</summary>
    public MetaSession Session => new(Messages, Config, Sessions);

    /// <summary>The stop-check sweep.</summary>
    public MetaSweep Sweep => new(Sync, Refresher, Reader);

    /// <summary>The loop guard.</summary>
    public StopGuard Guard => new(Values, Log);

    /// <summary>Saves a config for team "t1" with the lanes meta, api and ui, and registers the meta session.</summary>
    /// <returns>A task.</returns>
    public async Task Ready()
    {
        _ = await Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta"), CancellationToken.None);
        _ = await Messages.SetRole(new MetaRole(SessionId.From(MetaSessionId), "t1-meta"), CancellationToken.None);
    }

    /// <summary>Saves a config with a repo at /work, a "repo" scope (always; sync-rules) and a "dotnet" scope (src/**, tests/**; heavy build and unit), docs-only paths, a merge order #190 before #192 and one known flake.</summary>
    /// <returns>A task.</returns>
    public async Task ReadyWithGate()
    {
        await Ready();
        var gate = new GateConfig(
            ["repo"],
            ["**/*.md", "docs/**"],
            new Dictionary<string, ScopeConfig>(StringComparer.Ordinal)
            {
                ["repo"] = new(["**"], [new StepConfig("sync-rules", ["scripts/sync-rules.sh", "--check"])]),
                ["dotnet"] = new(["src/**", "tests/**"], [new StepConfig("build", ["dotnet", "build", "X.slnx"], Heavy: true), new StepConfig("unit", ["dotnet", "test", "--solution", "X.slnx"], Heavy: true)]),
            });
        _ = await Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Gate = gate, MergeOrder = [new MergeOrderRule(190, 192)], KnownFlakes = ["SpikeEvalTests"] }, CancellationToken.None);
    }

    /// <summary>The open obligations now.</summary>
    /// <returns>The open set.</returns>
    public async Task<IReadOnlyList<Obligation>> Open() =>
        (await Reader.Open("meta", Clock.GetUtcNow(), CancellationToken.None)).Value;

    /// <summary>A transcript line holding a peer message.</summary>
    /// <param name="id">The message id.</param>
    /// <param name="name">The sender's session name.</param>
    /// <param name="body">The text.</param>
    /// <returns>The JSONL line with its line ending.</returns>
    public static string PeerLine(string id, string name, string body) =>
        $$"""{"type":"user","origin":{"kind":"peer","from":"uds:/run/{{name}}.sock","msg_id":"{{id}}","name":"{{name}}","body":"{{body}}"},"timestamp":"2026-10-15T04:05:00.000Z"}""" + "\n";

    /// <summary>Appends a peer message to the meta transcript and syncs it into the inbox (appended, so the sync offset stays valid across calls).</summary>
    /// <param name="id">The message id.</param>
    /// <param name="name">The sender's session name.</param>
    /// <param name="body">The text.</param>
    /// <returns>A task.</returns>
    public async Task Receive(string id, string name, string body)
    {
        Transcripts.Append(Transcript, PeerLine(id, name, body));
        _ = await Sync.Run(Transcript, CancellationToken.None);
    }

    /// <summary>A transcript line holding a peer message that arrived mid-turn: the queued_command attachment shape.</summary>
    /// <param name="id">The message id; empty for none (the delivery id then names it).</param>
    /// <param name="name">The sender's session name.</param>
    /// <param name="body">The text.</param>
    /// <param name="deliveryId">The delivery id.</param>
    /// <returns>The JSONL line with its line ending.</returns>
    public static string QueuedLine(string id, string name, string body, string deliveryId = "d-1")
    {
        var msgId = id.Length > 0 ? "\"msg_id\":\"" + id + "\"," : string.Empty;
        return $$$"""{"type":"attachment","attachment":{"type":"queued_command","prompt":"p","delivery_id":"{{{deliveryId}}}","origin":{"kind":"peer","from":"uds:/run/{{{name}}}.sock",{{{msgId}}}"name":"{{{name}}}","body":"{{{body}}}"}},"timestamp":"2026-10-15T04:05:00.000Z"}""" + "\n";
    }

    /// <summary>Records a non-Claude lane with a session, as the launcher and the session-id hook would, with its tmux session running the agent.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="agent">The catalog id (the process name is the same).</param>
    /// <param name="hooks">Whether the CLI has hook support.</param>
    /// <param name="recordedMinute">Minutes after T0 the session id was recorded.</param>
    /// <returns>The identity the lane's hooks run as.</returns>
    public LaneIdentity PutLane(string lane, string agent = "codex", bool hooks = true, int recordedMinute = 10)
    {
        LaneFiles.Put(new LaneRecord(lane, SessionId.From("sess-" + lane), new LaneAgent(agent, agent, hooks), A.At(recordedMinute)));
        Panes.Put("t1-" + lane, agent);
        return new LaneIdentity("t1", lane);
    }
}
