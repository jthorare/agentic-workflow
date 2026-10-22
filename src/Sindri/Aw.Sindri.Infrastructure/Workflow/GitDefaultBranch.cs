using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IDefaultBranch"/> through git: <c language="text">git symbolic-ref --short refs/remotes/origin/HEAD</c> with the 'origin/' stripped.</summary>
internal sealed class GitDefaultBranch : IDefaultBranch
{
    private const string RemotePrefix = "origin/";

    public async ValueTask<Result<string>> Of(string checkout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        var run = await GitProcess.Run(checkout, ["symbolic-ref", "--short", "refs/remotes/origin/HEAD"], cancellationToken).ConfigureAwait(false);
        var name = run.Output.Trim();
        if (run.Exit != 0 || !name.StartsWith(RemotePrefix, StringComparison.Ordinal) || name.Length == RemotePrefix.Length)
        {
            return WorkflowErrors.EffectFailed.With($"{checkout} has no default branch git can name (origin/HEAD is not set): {run.Error.Trim()}");
        }

        return Results.Success(name[RemotePrefix.Length..]);
    }
}
