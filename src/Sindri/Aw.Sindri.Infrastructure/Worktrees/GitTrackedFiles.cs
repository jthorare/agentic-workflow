using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Worktrees;

/// <summary><see cref="ITrackedFiles"/> through git: <c language="text">git ls-files -z</c>.</summary>
internal sealed class GitTrackedFiles : ITrackedFiles
{
    public async ValueTask<Result<IReadOnlyList<string>>> Paths(string checkout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        var run = await GitProcess.Run(checkout, ["ls-files", "-z"], cancellationToken).ConfigureAwait(false);
        if (run.Exit != 0)
        {
            return WorkflowErrors.EffectFailed.With($"git ls-files failed in {checkout}: {run.Error.Trim()}");
        }

        IReadOnlyList<string> paths = [.. run.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return Results.Success(paths);
    }
}
