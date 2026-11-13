using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartSession;

/// <summary>What the checks of a start settled: the repo's folder and the agent to run.</summary>
/// <param name="RepoPath">The repo's folder.</param>
/// <param name="Agent">The catalog entry.</param>
internal sealed record StartPlan(string RepoPath, AgentEntry Agent);
