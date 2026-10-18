using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass over the sources (collector spec §4.1): the process table, Claude's records, the hook's state files. A source that fails is a problem row and leaves the
/// others intact; when the process table itself fails or overruns the pass is not good and the caller keeps the previous snapshot.
/// </summary>
internal sealed class SessionDiscovery(IProcessTable processes, IClaudeSessionRecords records, IFleetStates fleet, IAgentCatalog catalog, SessionSettings settings, SessionHosts hosts, TimeProvider time)
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly IReadOnlyDictionary<string, string> _emptyEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>False when launcher.json turned the collector off.</summary>
    internal bool IsEnabled() => settings.Enabled;

    /// <summary>The problem rows every snapshot carries: a test override in this run, an invalid launcher.json.</summary>
    internal IReadOnlyList<SessionProblem> StaticProblems()
    {
        var problems = new List<SessionProblem>();
        if (settings.TestOverride)
        {
            problems.Add(SessionProblems.TestOverride);
        }

        if (settings.ConfigProblem is not null)
        {
            problems.Add(SessionProblems.Launcher);
        }

        return problems;
    }

    /// <summary>Deletes dead sessions' hook files after a good pass (collector spec §5.4).</summary>
    internal int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now) => fleet.Prune(live, now);

    /// <summary>The sources still running from an earlier pass that gave up on them: by name, so a blocked source is reported again and never started a second time.</summary>
    private readonly Dictionary<string, Task> _abandoned = new(StringComparer.Ordinal);

    /// <summary>
    /// One pass. The process table gets the whole <paramref name="budget"/> (past it, null: the caller keeps the previous snapshot); every other source gets its own
    /// fifth of it, so one slow source is one problem row and the rest stay live (collector spec §4.1).
    /// </summary>
    internal Discovered? Discover(TimeSpan budget, CancellationToken cancellationToken)
    {
        var problems = new List<SessionProblem>();
        var pass = new SourcePass(_abandoned, budget, problems, time, cancellationToken);
        var (outcome, table) = pass.Run("processes", pid: null, processes.Read);
        if (outcome == SourceOutcome.TimedOut)
        {
            return null;
        }

        if (table is null)
        {
            return new Discovered([], [SessionProblems.Source("processes")], Good: false);
        }

        if (table.IsError)
        {
            return new Discovered([], [SessionProblems.ProcessTable(table.Error)], Good: false);
        }

        Prune(table.Value);
        return Assemble(table.Value, pass, problems);
    }

    private Discovered Assemble(IReadOnlyList<ProcessEntry> table, SourcePass pass, List<SessionProblem> problems)
    {
        var agents = Agents();
        var claudeRecords = pass.List("claude-records", records.Read);
        var hooks = pass.List("fleet-states", fleet.Read);
        var byPid = table.ToDictionary(static p => p.Pid);
        var live = LiveRecords(claudeRecords, byPid);
        var unknown = new HashSet<(int Pid, long StartTicks)>();
        var found = AgentProcessRules.Find(table, agents, pid => IsJudgeChild(byPid[pid], pass, unknown), live.ContainsKey);
        var candidates = new List<Candidate>();
        if (agents.Find(static a => a.Id.Equals(_claude)) is { } claudeAgent)
        {
            candidates.AddRange(live.Values.Select(r => FromRecord(r, byPid[r.Pid], claudeAgent, hooks, pass)));
        }

        candidates.AddRange(found.Where(f => !live.ContainsKey(f.Process.Pid)).Select(f => FromProcess(f, hooks, pass)));
        var sources = hosts.ForPass(pass);
        return new Discovered([.. candidates.Select(c => c with { Host = SessionHosts.Detect(sources, byPid, c) })], problems, Good: true) { Unknown = unknown };
    }

    /// <summary>A process whose environment can't be read is unknown, never "not a judge child": it is left out of this pass and its previous state is kept.</summary>
    private bool IsJudgeChild(ProcessEntry process, SourcePass pass, HashSet<(int Pid, long StartTicks)> unknown)
    {
        var pid = process.Pid;
        var (outcome, environment) = pass.Run("environment", pid, _ => processes.Environment(pid));
        if (outcome == SourceOutcome.Done && environment is not null)
        {
            return environment.ContainsKey("AW_JUDGE_CHILD");
        }

        pass.Note("environment");
        _ = unknown.Add((pid, process.StartTicks));
        return true;   // left out of this pass either way; the service keeps what it showed before
    }

    /// <summary>Completes when every abandoned read has returned (tests).</summary>
    internal Task AbandonedSettled() => Task.WhenAll(_abandoned.Values);

    /// <summary>How many abandoned reads are remembered (tests).</summary>
    internal int AbandonedCount => _abandoned.Count;

    /// <summary>Forgets reads that finished and per-pid reads of processes that are gone, so the map follows the table instead of growing with pid churn.</summary>
    private void Prune(IReadOnlyList<ProcessEntry> table)
    {
        var pids = table.Select(static p => p.Pid).ToHashSet();
        foreach (var (key, task) in _abandoned.ToList())
        {
            var colon = key.IndexOf(':', StringComparison.Ordinal);
            var gone = colon > 0 && int.TryParse(key.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && !pids.Contains(pid);
            if (task.IsCompleted || gone)
            {
                _ = _abandoned.Remove(key);
            }
        }
    }

    private static Dictionary<int, ClaudeRecord> LiveRecords(IReadOnlyList<ClaudeRecord> records, Dictionary<int, ProcessEntry> byPid)
    {
        var live = new Dictionary<int, ClaudeRecord>();
        foreach (var record in records)
        {
            if (byPid.TryGetValue(record.Pid, out var process) && SessionLiveness.Matches(record.ProcStart, process.StartTicks) && IsSession(record))
            {
                live[record.Pid] = record;
            }
        }

        return live;
    }

    private static bool IsSession(ClaudeRecord record) =>
        string.Equals(record.Kind, "interactive", StringComparison.Ordinal)
        || (string.Equals(record.Entrypoint, "sdk-ts", StringComparison.Ordinal) && record.Cwd?.Contains("/.t3/worktrees", StringComparison.Ordinal) == true);

    private List<AgentEntry> Agents() =>
        [.. catalog.All.Where(a => settings.Providers is null || settings.Providers.Any(p => p.Equals(a.Id)))];

    private Candidate FromRecord(ClaudeRecord record, ProcessEntry process, AgentEntry agent, IReadOnlyList<FleetStateFile> hooks, SourcePass pass)
    {
        var id = record.Session ?? SessionId.OfProcess(process.Pid, process.StartTicks);
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(agent.Id) && h.Session.Equals(id));
        return new Candidate(process, agent, id, IdKnown: record.Session is not null, pass.Value("environment", process.Pid, _ => processes.Environment(process.Pid), _emptyEnvironment))
        {
            Record = record,
            Hook = hook,
            Cwd = record.Cwd ?? hook?.Cwd ?? pass.Value("cwd", process.Pid, _ => processes.Cwd(process.Pid), fallback: null),
            TranscriptHint = hook?.Transcript,
        };
    }

    private Candidate FromProcess(AgentProcess found, IReadOnlyList<FleetStateFile> hooks, SourcePass pass)
    {
        var process = found.Process;
        var hook = hooks.FirstOrDefault(h => h.Provider.Equals(found.Agent.Id) && h.AgentPid == process.Pid && h.AgentStartTicks == process.StartTicks);
        var files = pass.Value("open-files", process.Pid, _ => processes.OpenFiles(process.Pid), []);
        var known = hook?.Session ?? RolloutId(found.Agent, files);
        return new Candidate(process, found.Agent, known ?? SessionId.OfProcess(process.Pid, process.StartTicks), IdKnown: known is not null, pass.Value("environment", process.Pid, _ => processes.Environment(process.Pid), _emptyEnvironment))
        {
            Hook = hook,
            Cwd = hook?.Cwd ?? pass.Value("cwd", process.Pid, _ => processes.Cwd(process.Pid), fallback: null),
            TranscriptHint = hook?.Transcript ?? files.FirstOrDefault(static f => f.EndsWith(".jsonl", StringComparison.Ordinal)),
        };
    }

    private static SessionId? RolloutId(AgentEntry agent, IReadOnlyList<string> files)
    {
        if (agent.IdCapture is not { } pattern)
        {
            return null;
        }

        var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var file in files)
        {
            var match = regex.Match(file);
            if (match.Success && match.Groups.Count > 1 && SessionId.TryFrom(match.Groups[1].Value, out var id))
            {
                return id;
            }
        }

        return null;
    }
}
