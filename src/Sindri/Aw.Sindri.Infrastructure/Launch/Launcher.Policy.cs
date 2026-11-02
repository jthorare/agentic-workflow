using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A Step's policy file (the part of <see cref="Launcher"/> that writes the file the entry's policy form reads).</summary>
internal sealed partial class Launcher
{
    /// <summary>The policy file the entry's form needs, written for this launch; null when the form has none. A cursor-config Step is refused when the worktree already has the repo's own .cursor/cli.json or one a live launch wrote; a dead launch's is replaced.</summary>
    private async ValueTask<Result<string?>> PolicyFile(AgentEntry entry, LaunchId launch, string workDir, StepPolicy policy, CancellationToken cancellationToken) => entry.PolicyForm switch
    {
        PolicyForm.ClaudeSettingsFile => Results.Success<string?>(StepPolicyFile.Write(parts.Options.PolicyDirectory, launch, policy)),
        PolicyForm.CursorConfig => await CursorFile(launch, workDir, policy, cancellationToken).ConfigureAwait(false),
        PolicyForm.None or PolicyForm.CodexOverrides => Results.Success<string?>(value: null),
        _ => Results.Success<string?>(value: null),
    };

    // The config goes into the checkout, so it is git-excluded first (the same local 'info/exclude' mechanism as the Step's fact files) and the Step's 'git add' never commits it.
    private async ValueTask<Result<string?>> CursorFile(LaunchId launch, string workDir, StepPolicy policy, CancellationToken cancellationToken)
    {
        if (await StepFactWriter.Excluded(workDir, [".cursor/cli.json"], cancellationToken).ConfigureAwait(false) is { } excluded)
        {
            return excluded;
        }

        var path = CursorPolicyFile.PathIn(workDir);
        var holder = CursorPolicyFile.Holder(parts.Options.PolicyDirectory, path);
        var holderLive = holder is { } h && await parts.IsLive(h, cancellationToken).ConfigureAwait(false);   // the same test the sweep uses: the launch's view row is live
        var written = CursorPolicyFile.Write(parts.Options.PolicyDirectory, launch, workDir, policy, id => holderLive && id == holder);
        return written.IsError ? written.Error : Results.Success<string?>(written.Value);
    }
}
