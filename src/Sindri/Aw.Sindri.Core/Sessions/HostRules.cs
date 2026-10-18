using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Host detection (collector spec §6.1): which host owns and shows a session, from its process chain and what the host's own tools say.</summary>
public static partial class HostRules
{
    private const string TermProgramWarp = "WarpTerminal";

    /// <summary>Finds the host of one session.</summary>
    /// <param name="query">The session's process, table and record.</param>
    /// <param name="sources">What it may ask the world for.</param>
    /// <returns>The host; 'unknown' when no ancestor names one.</returns>
    public static HostInfo Detect(HostQuery query, IHostSources sources)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sources);
        var chain = HostChain.Ancestors(query.Table, query.Agent);
        var hit = HostChain.Classify(chain, sources.Terminals, allowTmux: true);
        if (hit is null)
        {
            return Fallback(query);
        }

        return hit.Kind switch
        {
            HostKind.Tmux => Tmux(query, chain, hit, sources),
            HostKind.Ssh => new HostInfo("ssh", "ssh"),
            HostKind.Ide => new HostInfo("ide", hit.Name) { Ide = hit.Name },
            HostKind.Terminal or HostKind.Warp => Terminal(hit, sources) is { } terminal ? Shown(terminal) : HostInfo.Unknown,
            _ => HostInfo.Unknown,
        };
    }

    /// <summary>The kind of a tmux socket: 'sindri' for a path ending /sindri or /sindri-launch (the launcher's own socket), 'default' for /default, 'team' for a team's lane session or a socket named for a team, else 'other'.</summary>
    /// <param name="socket">The socket path, or null.</param>
    /// <param name="session">The tmux session name, or null.</param>
    /// <param name="teams">The Meta teams.</param>
    /// <returns>The kind.</returns>
    public static string SocketKind(string? socket, string? session, IReadOnlyList<TeamNames> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        var name = socket?[(socket.LastIndexOf('/') + 1)..];
        if (string.Equals(name, "sindri", StringComparison.Ordinal) || string.Equals(name, "sindri-launch", StringComparison.Ordinal))
        {
            return "sindri";
        }

        if (string.Equals(name, "default", StringComparison.Ordinal))
        {
            return "default";   // the owner's lanes live on the default socket: that stays 'default' even when the session is named <team>-<lane>
        }

        var named = name is not null && teams.Any(t => string.Equals(t.Team, name, StringComparison.Ordinal));
        return named || (session is not null && teams.Any(t => t.Lanes.Any(l => string.Equals(session, t.Team + "-" + l, StringComparison.Ordinal)))) ? "team" : "other";
    }

    private static HostInfo Fallback(HostQuery query)
    {
        var env = query.AgentEnvironment;
        if (env.TryGetValue("TERM_PROGRAM", out var program) && string.Equals(program, TermProgramWarp, StringComparison.Ordinal))
        {
            return Shown(new TerminalFacts("warp", 0) { WarpFocusUrl = FocusUrl(env) });
        }

        return query.T3 ? new HostInfo("t3", "t3") : HostInfo.Unknown;
    }

    private static HostInfo Shown(TerminalFacts terminal) => terminal.Name switch
    {
        "warp" => new HostInfo("warp", "Warp") { Terminal = terminal },
        _ => new HostInfo("terminal", terminal.Name) { Terminal = terminal },
    };

    /// <summary>The terminal facts of a hit, read from the shell under it; a Warp shell is Warp whichever process name the terminal runs under.</summary>
    private static TerminalFacts? Terminal(HostHit hit, IHostSources sources)
    {
        var env = hit.Shell is { } shell ? sources.Environment(shell.Pid) : new Dictionary<string, string>(StringComparer.Ordinal);
        var warp = hit.Kind == HostKind.Warp || (env.TryGetValue("TERM_PROGRAM", out var program) && string.Equals(program, TermProgramWarp, StringComparison.Ordinal));
        return warp
            ? new TerminalFacts("warp", hit.Process.Pid) { WarpFocusUrl = FocusUrl(env) }
            : new TerminalFacts(hit.Name, hit.Process.Pid)
            {
                XWindow = env.GetValueOrDefault("WINDOWID"),
                KittyWindow = env.GetValueOrDefault("KITTY_WINDOW_ID"),
                KittyListenOn = env.GetValueOrDefault("KITTY_LISTEN_ON"),
                WeztermPane = env.GetValueOrDefault("WEZTERM_PANE"),
            };
    }

    private static string? FocusUrl(IReadOnlyDictionary<string, string> env) =>
        env.TryGetValue("WARP_FOCUS_URL", out var url) && WarpUrl.IsMatch(url) ? url : null;

    private static HostInfo Tmux(HostQuery query, IReadOnlyList<ProcessEntry> chain, HostHit hit, IHostSources sources)
    {
        var socket = SocketOf(query, hit.Process.Pid, sources);
        var view = socket is null ? null : sources.View(socket);
        var pane = view is null ? null : PaneOf(view, query.Agent, chain);
        var (session, paneId) = pane is not null ? (pane.Session, pane.Pane) : FromRecord(query.RecordTmux);
        var terminal = view is null || session is null ? null : ClientTerminal(query, view, session, sources);
        var clients = view is null || session is null ? (int?)null : view.Clients.Count(c => string.Equals(c.Session, session, StringComparison.Ordinal));
        var facts = new TmuxFacts(socket, SocketKind(socket, session, sources.Teams), session, paneId) { AttachedClients = clients };
        return new HostInfo("tmux", terminal is null ? "tmux" : "tmux · " + Label(terminal)) { Tmux = facts, Terminal = terminal };
    }

    /// <summary>The socket the proc table gives; the agent's own TMUX only fills in when the table gives none and the server pid in it is this server's.</summary>
    private static string? SocketOf(HostQuery query, int serverPid, IHostSources sources)
    {
        if (sources.SocketOf(serverPid) is { } found)
        {
            return found;
        }

        var parts = query.AgentEnvironment.GetValueOrDefault("TMUX")?.Split(',');
        return parts is { Length: >= 2 } && parts[0].StartsWith('/') && int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) && pid == serverPid ? parts[0] : null;
    }

    private static TmuxPane? PaneOf(TmuxServerView view, ProcessEntry agent, IReadOnlyList<ProcessEntry> chain) =>
        view.Panes.FirstOrDefault(p => p.PanePid == agent.Pid) ?? chain.Select(c => view.Panes.FirstOrDefault(p => p.PanePid == c.Pid)).FirstOrDefault(static p => p is not null);

    /// <summary>The record's '&lt;session&gt;:@&lt;window&gt;.%&lt;pane&gt;' alone, when no tmux answered.</summary>
    private static (string? Session, string? Pane) FromRecord(string? field)
    {
        var colon = field?.LastIndexOf(":@", StringComparison.Ordinal) ?? -1;
        if (field is null || colon <= 0)
        {
            return (null, null);
        }

        var percent = field.LastIndexOf(".%", StringComparison.Ordinal);
        return (field[..colon], percent > colon ? field[(percent + 1)..] : null);
    }

    /// <summary>Terminal facts come only from a client's own chain: the agent's TERM_PROGRAM and WARP_* are inherited through the tmux server and say nothing.</summary>
    private static TerminalFacts? ClientTerminal(HostQuery query, TmuxServerView view, string session, IHostSources sources)
    {
        foreach (var client in view.Clients.Where(c => string.Equals(c.Session, session, StringComparison.Ordinal)))
        {
            if (!query.Table.TryGetValue(client.Pid, out var process))
            {
                continue;
            }

            var hit = HostChain.Classify(HostChain.Ancestors(query.Table, process), sources.Terminals, allowTmux: false);
            if (hit is not null && hit.Kind != HostKind.Ide && Terminal(hit, sources) is { } terminal)
            {
                return terminal;
            }
        }

        return null;
    }

    private static string Label(TerminalFacts terminal) => string.Equals(terminal.Name, "warp", StringComparison.Ordinal) ? "Warp" : terminal.Name;

    [GeneratedRegex("^warp://session/[0-9a-f]{32}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WarpUrl { get; }
}
