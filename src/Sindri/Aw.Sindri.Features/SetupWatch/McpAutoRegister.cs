using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The automatic registration of Sindri's MCP address (setup-once spec §5.4 (a) and (b)): for each repo of the approved snapshot whose providers are not all registered for the running port, a RegisterMcp job goes through
/// the setup queue (one at a time). A registration that is already there starts nothing. It does nothing until <see cref="McpAutoGate"/> lets it.
/// </summary>
/// <param name="gate">Says whether the automatic runs may go ahead.</param>
/// <param name="need">Says whether a registration would change anything.</param>
/// <param name="queue">The single slot of the setup service's jobs.</param>
/// <param name="starter">Starts a job.</param>
/// <param name="running">Which jobs the runner has now.</param>
internal sealed class McpAutoRegister(McpAutoGate gate, IMcpNeed need, SetupJobQueue queue, IMcpJobStarter starter, IRunningJobs running)
{
    private static readonly JobKind _kind = JobKind.From("RegisterMcp");

    /// <summary>Starts the jobs the approved snapshot's repos need.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that ends when every needed job was handed to the queue.</returns>
    internal async ValueTask Run(CancellationToken cancellationToken)
    {
        if (await gate.Ready(cancellationToken).ConfigureAwait(false) is not { } approved)
        {
            return;
        }

        if (queue.Slot is { } held && !running.IsRunning(held.Kind, held.Key, out _))
        {
            await queue.Finished(held.Kind, held.Key, cancellationToken).ConfigureAwait(false);   // the job that held the slot has ended
        }

        foreach (var entry in approved.Profile.Repos ?? [])
        {
            if (RepoName.TryFrom(entry.Name, out var repo) && !await gate.RecentlyFailed(repo, cancellationToken).ConfigureAwait(false) && await need.Needed(repo, cancellationToken).ConfigureAwait(false))
            {
                _ = await queue.Submit(_kind, McpJobKeys.For(repo), ct => starter.Start(repo, ct), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
