using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>A live agent process with everything the pass learned about it, before its state is merged.</summary>
/// <param name="Process">The process.</param>
/// <param name="Agent">Its catalog entry.</param>
/// <param name="Session">The session id, or the 'pid-…' id when none is known.</param>
/// <param name="IdKnown">True when the id is the agent's own.</param>
/// <param name="Environment">Its allowlisted environment.</param>
internal sealed record Candidate(ProcessEntry Process, AgentEntry Agent, SessionId Session, bool IdKnown, IReadOnlyDictionary<string, string> Environment)
{
    internal ClaudeRecord? Record { get; init; }

    internal FleetStateFile? Hook { get; init; }

    internal string? Cwd { get; init; }

    internal string? TranscriptHint { get; init; }

    internal HostInfo Host { get; init; } = HostInfo.Unknown;

    internal SessionRef Ref => new(Agent.Id, Session);
}
