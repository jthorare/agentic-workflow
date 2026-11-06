using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class Launcher
{
    /// <summary>What <see cref="Prepare"/> made: the working directory, the confined argv and environment, and what the outcome records.</summary>
    private sealed record Prepared(string WorkDir, IReadOnlyList<string> Argv, Dictionary<string, string> Env, string? Session)
    {
        /// <summary>The hash file as it was before this start rewrote it, or null when there was none.</summary>
        public byte[]? PriorHash { get; init; }

        /// <summary>The first message, for a host that runs the agent itself (T3) rather than the argv.</summary>
        public string? Seed { get; init; }

        /// <summary>The sandbox summary of a confined Step launch (<c lang="text">launch.started</c>'s sandbox field), or null when it ran unconfined.</summary>
        public SandboxSummary? Sandbox { get; init; }

        /// <summary>The confined launch's run directory, where <c lang="text">wrap-limits.json</c> sits, or null when it ran unconfined.</summary>
        public string? SandboxRunArea { get; init; }

        /// <summary>The sha of the PR head fetched for a skill Step's 'pr' input, or null.</summary>
        public string? PrHead { get; init; }
    }
}
