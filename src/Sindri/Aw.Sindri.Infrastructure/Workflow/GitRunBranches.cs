using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Reads whether a run branch kept work with git: commits on the branch that the checkout's current branch lacks.</summary>
internal sealed class GitRunBranches : IRunBranches
{
    public async ValueTask<bool> HasCommits(string repository, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(branch);
        var counted = await GitProcess.Run(repository, ["rev-list", "--count", "HEAD.." + "refs/heads/" + branch, "--"], cancellationToken).ConfigureAwait(false);
        return counted.Exit == 0 && int.TryParse(counted.Output.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ahead) && ahead > 0;
    }
}
