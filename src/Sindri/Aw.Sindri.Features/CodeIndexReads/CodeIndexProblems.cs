using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// The health row a repo's code index makes, if any (the Health card's words, code index spec §7.3): damaged, a build retrying after failures, a failed code graph, or an index behind main.
/// Missing, building, waiting for a build slot, a first build queued under 30 min and pending embeddings are not problems. At most one row a repo: damaged, then retrying, then unavailable, then queued too long, then stale.
/// </summary>
internal static class CodeIndexProblems
{
    private const string Retrying = "retrying in ";

    /// <summary>How long a first build may stay queued before it is a problem (#3440): code-index-main-move ticks every minute and holds a repo up to 10 min, so a normal wait ends well inside it.</summary>
    internal const int QueuedLimitMinutes = 30;

    /// <summary>The repo's problem.</summary>
    /// <param name="status">The repo's index status, from the files.</param>
    /// <param name="health">The same repo's health, with the heads and jobs laid over it.</param>
    /// <returns>The problem, or null when the index is fine or only on its way.</returns>
    internal static HealthProblem? For(RepoStatus status, RepoHealth health)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(health);
        var repo = health.Repo;
        if (health.State == "damaged")
        {
            return new(CodeIndexErrors.IndexDamaged.Code, $"{repo}: code index is damaged; it rebuilds on the next check", $"the code index for {repo} is damaged");
        }

        if (health.State == "waiting" && health.Waiting is { } waiting && waiting.StartsWith(Retrying, StringComparison.Ordinal))
        {
            return new(CodeIndexErrors.BuildFailed.Code, $"{repo}: code index build failed; {waiting}", $"the code index for {repo} is retrying a build");
        }

        // Only a graph that failed with a code: a layer this version didn't build has no code, and the embedding server is doctor's row.
        if (status.Layers.FirstOrDefault(static l => l is { Layer: LayerName.Graph, State: LayerState.Unavailable, Code: not null }) is { Code: { } code })
        {
            return new(code, $"{repo}: code graph is unavailable", $"the code index for {repo} is unavailable");
        }

        if (health.State == "queued" && health.QueuedFor >= TimeSpan.FromMinutes(QueuedLimitMinutes))
        {
            return new(CodeIndexErrors.IndexNotBuilt.Code, $"{repo}: the first code index build hasn't started in {QueuedLimitMinutes} min", $"the code index for {repo} hasn't started its first build");
        }

        return health.State == "stale"
            ? new(CodeIndexErrors.IndexBehind.Code, $"{repo}: code index is behind main; rebuild it from the System page", $"the code index for {repo} is behind main")
            : null;
    }
}
