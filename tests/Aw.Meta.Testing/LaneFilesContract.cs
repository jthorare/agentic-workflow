using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ILaneFiles contract, run against the in-memory fake and the directory adapter.</summary>
public abstract class LaneFilesContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh store plus a way to put a lane's record in place the way the launcher and the hook do.</summary>
    /// <returns>The store and the writer.</returns>
    protected abstract ValueTask<(ILaneFiles Files, Func<LaneRecord, Task> Put)> Create();

    /// <summary>A lane nothing was recorded for has no record.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_lane_with_nothing_recorded_has_no_record()
    {
        var (files, _) = await Create();

        var record = await files.Record("api", _none);

        Expect.That(record.IsSuccess && record.Value is null, "no record");
    }

    /// <summary>The session id, agent facts and ambiguity read back.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_recorded_lane_reads_back_with_its_session_and_agent()
    {
        var (files, put) = await Create();
        await put(new LaneRecord("api", Core.Ids.SessionId.From("s-1"), new LaneAgent("codex", "codex", Hooks: true, "~/.codex/{sessionId}.jsonl")));
        await put(new LaneRecord("ui", Session: null, new LaneAgent("aider", "aider", Hooks: false), RecordedAt: null, Ambiguous: true));

        var api = await files.Record("api", _none);
        var ui = await files.Record("ui", _none);

        var a = api.Value!;
        var u = ui.Value!;
        Expect.That(a.Session?.Value == "s-1" && a.IsNonClaude, "session and a non-Claude agent");
        Expect.That(string.Equals(a.Agent?.Agent, "codex", StringComparison.Ordinal) && string.Equals(a.Agent?.ProcessName, "codex", StringComparison.Ordinal) && a.Agent!.Hooks, "agent facts");
        Expect.That(string.Equals(a.Agent?.Transcripts, "~/.codex/{sessionId}.jsonl", StringComparison.Ordinal), "the transcript glob");
        Expect.That(u.Session is null && !u.Agent!.Hooks && u.Ambiguous, "ambiguous with no session and no hooks");
    }

    /// <summary>RecordSession replaces the id, keeps the agent facts and refuses a name that could escape the directory.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Recording_a_session_replaces_the_id_keeps_the_agent_and_refuses_an_escaping_name()
    {
        var (files, put) = await Create();
        await put(new LaneRecord("api", Core.Ids.SessionId.From("s-1"), new LaneAgent("codex", "codex", Hooks: true)));

        var fresh = await files.RecordSession("api", Core.Ids.SessionId.From("s-2"), DateTimeOffset.UnixEpoch.AddDays(20000), _none);
        var brandNew = await files.RecordSession("ui", Core.Ids.SessionId.From("s-3"), DateTimeOffset.UnixEpoch.AddDays(20000), _none);
        var escaping = await files.RecordSession("../x", Core.Ids.SessionId.From("s-4"), DateTimeOffset.UnixEpoch.AddDays(20000), _none);

        var api = (await files.Record("api", _none)).Value!;
        var ui = (await files.Record("ui", _none)).Value!;
        Expect.That(fresh.IsSuccess && brandNew.IsSuccess, "both recorded");
        Expect.That(api.Session?.Value == "s-2" && string.Equals(api.Agent?.Agent, "codex", StringComparison.Ordinal), "the id is replaced and the agent kept");
        Expect.That(ui.Session?.Value == "s-3" && ui.Agent is null, "a lane with no record gets one with just the id");
        Expect.That(escaping.IsError && (await files.Record("../x", _none)).Value is null, "an escaping name is refused");
    }

    /// <summary>A lane whose hooks left no trace has none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_lane_with_no_hook_trace_has_none()
    {
        var (files, _) = await Create();

        var trace = await files.HookTrace("api", _none);

        Expect.That(trace.IsSuccess && trace.Value is null, "no trace");
    }

    /// <summary>A name that could escape the directory, or is not a lane name at all, has no trace.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_bad_lane_name_has_no_hook_trace()
    {
        var (files, _) = await Create();

        var escaping = await files.HookTrace("../api", _none);
        var dots = await files.HookTrace("..", _none);
        var empty = await files.HookTrace(string.Empty, _none);

        Expect.That(escaping.IsSuccess && escaping.Value is null && dots.IsSuccess && dots.Value is null && empty.IsSuccess && empty.Value is null, "no trace for a bad name");
    }
}
