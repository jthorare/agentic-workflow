using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The aw:fleet hook's state files (collector spec §4.1 step 4, §5.4): read from &lt;root&gt;/&lt;provider&gt;/*.json for the discovered providers, and pruned
/// here and nowhere else. A file only counts when it validates against the schema's rules.
/// </summary>
/// <param name="root">The fleet directory ($AW_FLEET_DIR, default $AW_STATE_DIR/fleet).</param>
/// <param name="providers">The providers whose directories are read and pruned.</param>
/// <param name="pruning">False under a test override: nothing is deleted.</param>
internal sealed class FleetStateFiles(string root, IReadOnlyList<AgentId> providers, bool pruning) : IFleetStates
{
    internal const string Marker = ".aw-fleet";
    internal const int MaxPerProvider = 512;
    internal static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SessionsJsonContext.Default);

    public Result<IReadOnlyList<FleetStateFile>> Read(CancellationToken cancellationToken)
    {
        var found = new List<FleetStateFile>();
        foreach (var provider in providers)
        {
            found.AddRange(Valid(provider).Select(static v => v.State));
        }

        return found;
    }

    public int Prune(IReadOnlyCollection<SessionRef> live, DateTimeOffset now)
    {
        if (!pruning || InsideGitWorkTree(root))
        {
            return 0;
        }

        var deleted = 0;
        foreach (var provider in providers.Where(p => Directory.Exists(Path.Combine(root, p.Value)) && Safe(Path.Combine(root, p.Value))))
        {
            var dead = Valid(provider).Where(v => !live.Contains(new SessionRef(provider, v.State.Session))).OrderBy(static v => v.State.At).ToList();
            var all = Valid(provider).Count;
            var overCap = Math.Max(0, all - MaxPerProvider);
            deleted += dead.Where((v, index) => now - v.State.At > Retention || index < overCap).Count(static v => Delete(v.Path));
        }

        return deleted;
    }

    private static bool Delete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool Safe(string directory) =>
        File.Exists(Path.Combine(directory, Marker))
        && (OperatingSystem.IsWindows() || File.GetUnixFileMode(directory) == (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));

    private static bool InsideGitWorkTree(string path)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(path)); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return true;
            }
        }

        return false;
    }

    private List<(FleetStateFile State, string Path)> Valid(AgentId provider)
    {
        var directory = Path.Combine(root, provider.Value);
        var files = new List<(FleetStateFile, string)>();
        string[] names;
        try
        {
            names = Directory.Exists(directory) ? [.. Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal).Take(MaxPerProvider * 2)] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return files;
        }

        foreach (var name in names)
        {
            if (One(name, provider) is { } state)
            {
                files.Add((state, name));
            }
        }

        return files;
    }

    private static FleetStateFile? One(string path, AgentId provider)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var read = AwJson.Deserialize<FleetStateDocument>(bytes, _json);
        return read.IsError ? null : Map(read.Value, provider);
    }

    private static FleetStateFile? Map(FleetStateDocument d, AgentId provider)
    {
        if (d.Schema != 1 || d.Provider != provider.Value || !SessionId.TryFrom(d.SessionId, out var session) || State(d.State) is not { } state
            || string.Equals(d.State, "asking", StringComparison.Ordinal) != (d.Reason is not null) || (d.Tool is not null && !string.Equals(d.Reason, "approval", StringComparison.Ordinal))
            || (d.AgentPid is null) != (d.AgentStartTicks is null))
        {
            return null;
        }

        return new FleetStateFile(provider, session, state, d.At)
        {
            Reason = d.Reason is null ? null : Reason(d.Reason),
            Tool = d.Tool,
            Cwd = d.Cwd,
            Transcript = d.Transcript,
            AgentPid = d.AgentPid,
            AgentStartTicks = d.AgentStartTicks,
        };
    }

    private static HookState? State(string text) => text switch
    {
        "running" => HookState.Running,
        "asking" => HookState.Asking,
        "idle" => HookState.Idle,
        "ended" => HookState.Ended,
        _ => null,
    };

    private static AskingReason? Reason(string text) => text switch
    {
        "approval" => AskingReason.Approval,
        "input" => AskingReason.Input,
        "auth" => AskingReason.Auth,
        _ => null,
    };
}
