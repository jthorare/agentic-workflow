using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Host detection for a pass (collector spec §6.1): wires the pure rules to the process table, the unix socket table, tmux and the team files, each read under the pass's own deadlines. It also reads this home's launch records, the other half of what scope 'own' calls own.</summary>
internal sealed class SessionHosts(IProcessTable processes, IUnixSockets sockets, ITmuxServers tmux, ITeamNames teams, ITerminalNames terminals, ILaunchFiles launches)
{
    /// <summary>Starts the reads of one pass; they are cached for that pass only (tmux keeps its own 10 s cache).</summary>
    /// <param name="pass">The pass, which bounds every read.</param>
    /// <returns>The pass's sources.</returns>
    internal PassHostSources ForPass(SourcePass pass) => new(pass, processes, sockets, tmux, teams, terminals);

    /// <summary>Finds a candidate's host.</summary>
    /// <param name="sources">The pass's sources.</param>
    /// <param name="table">The process table by pid.</param>
    /// <param name="candidate">The live session.</param>
    /// <returns>Its host.</returns>
    internal static HostInfo Detect(PassHostSources sources, IReadOnlyDictionary<int, ProcessEntry> table, Candidate candidate) =>
        HostRules.Detect(new HostQuery(table, candidate.Process, candidate.Environment) { RecordTmux = candidate.Record?.Tmux, T3 = candidate.Record?.IsT3 == true }, sources);

    /// <summary>This home's launch records. An unreadable folder is none, so a scratch home shows less, never more.</summary>
    /// <returns>The launch ids.</returns>
    internal IReadOnlySet<LaunchId> OwnLaunches()
    {
        try
        {
            return new HashSet<LaunchId>(launches.Launches());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new HashSet<LaunchId>();
        }
    }
}
