using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The launch's cleanup after a start (the part of <see cref="Launcher"/> that removes a launch's files and answers a name clash).</summary>
internal sealed partial class Launcher
{
    private async ValueTask Cleanup(LaunchId launch)
    {
        try
        {
            Directory.Delete(GhConfigPath(launch), recursive: true);   // a missing one is fine
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an empty directory left behind holds nothing
        }

        LaunchSecrets.Delete(parts.Options.SecretsDirectory, launch);
        LaunchFileStore.DeleteFlat(parts.Options, launch);
        LaunchFileStore.DeleteIn(parts.Options.PolicyDirectory, StepPolicyFile.PathFor(parts.Options.PolicyDirectory, launch));
        CursorPolicyFile.Delete(parts.Options.PolicyDirectory, launch);
        LaunchFileStore.DeleteIn(parts.Options.LaunchDirectory, McpLaunchConfig.PathFor(parts.Options.LaunchDirectory, launch));
        _ = await parts.Scratch.Remove(ScratchPath(launch), CancellationToken.None).ConfigureAwait(false);   // the evaluator's checkout; none for any other launch
        await ReleaseSandbox(launch).ConfigureAwait(false);
        if (_trialTrees.TryRemove(launch, out var trial))
        {
            await LaunchWorktree.Remove(trial.Repo, trial.Worktree, parts.Warn).ConfigureAwait(false);   // after the session is stopped: its files are no longer in use
        }
    }

    private string ScratchPath(LaunchId launch) => Path.Combine(parts.Options.ScratchDirectory, "eval-" + launch.Value);

    /// <summary>The start clashed with a session of this launch's name: it is the launch's own and stays untouched. The hash the live wrap checks is put back (and the session answered as started), or, with no hash to put back, the minted pair is removed and the launch fails.</summary>
    private LaunchResult Clash(LaunchRequest request, AgentEntry entry, ISessionHost host, Prepared prepared, Error clash)
    {
        LaunchSecrets.Delete(parts.Options.SecretsDirectory, request.Launch);
        if (prepared.PriorHash is not { } prior)
        {
            return Fail(clash);
        }

        OwnerOnly.WriteAtomically(LaunchSecrets.PathFor(parts.Options.SecretsDirectory, request.Launch), prior);
        var session = new HostRef(host.Kind, SessionName(request.Launch));
        return new StartedLaunch(session, ViewerOutcome.Skipped, host.AttachArgv(session), entry.Source, entry.ArgvHash ?? string.Empty);
    }
}
