using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Poll;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

/// <summary>A team t1 (lanes meta, api, ui) over the fakes, with the screens and transcripts a usage test puts in them. The clock is 05:00 UTC on 2026-10-15.</summary>
internal sealed class UsageWorld
{
    internal const string Limit = "  ⎿  You've hit your session limit · resets 5:30am (UTC)";
    internal const string LimitLine = """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"You've hit your session limit · resets 5:30am (UTC)"}]},"error":"rate_limit","isApiErrorMessage":true,"timestamp":"2026-10-15T04:58:00.000Z"}""";
    private static readonly string _rule = new('─', 40);

    internal MetaWorld World { get; } = new();

    internal UsageReader Reader => new(World.LaneFiles, World.LaneSessions, World.ClaudeTranscripts, World.Panes, World.Messages, World.Clock);

    internal LaneStandingReader Standings => new(World.LaneFiles, World.CodexRollouts, World.Clock);

    internal RouteWatcher Routes => new(Standings, World.Values);

    internal UsageMonitor Monitor => new(Reader, Watcher, Routes);

    internal UsageWatcher Watcher => new(new PaneSubmitter(World.Panes, World.Delay), World.Log, World.Values, World.Clock);

    internal static string Idle(string output) => $"{output}\n\n{_rule}\n❯ \n{_rule}\n  ⏵⏵ bypass permissions on (shift+tab to cycle)\n";

    internal static string Busy(string output) => $"{output}\n✻ Working… (4s · esc to interrupt)\n{_rule}\n❯ \n{_rule}\n";

    internal async Task Ready()
    {
        await World.Ready();
        World.Panes.Put("t1-api", "claude");
        World.Panes.Put("t1-ui", "claude");
        World.Panes.Put("t1-meta", "claude");
        World.Panes.Show("t1-api", Idle("⏺ fine"));
        World.Panes.Show("t1-ui", Idle("⏺ fine"));
        World.Panes.Show("t1-meta", Idle("⏺ fine"));
        World.ClaudeTranscripts.Put(MetaWorld.MetaSessionId, """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"ok"}]}}""" + "\n", A.At(55));
    }

    internal void LimitLane(string lane, bool busy = false) =>
        World.Panes.Show("t1-" + lane, busy ? Busy(Limit) : Idle(Limit));

    internal void LaneSession(string lane, string session)
    {
        World.LaneFiles.Put(new LaneRecord(lane, SessionId.From(session), Agent: null, A.At(1)));
        World.LaneSessions.Set("t1-" + lane, session);
    }
}
