using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The 'setup' built-in service (setup-once spec §5): it runs without a profile entry, so the scheduler runs it as 'service:setup' and PauseService refuses it (SND-SVC-404, as for
/// every service the approved profile doesn't name). This is its shell: woken by events and a 60-second backstop, a tick appends the 'repo.added' a profile entry is missing (SO-A3) and is otherwise quiet. The automatic MCP registration (SO-A4) runs here too; the other steps (SO-A6) fill it in.
/// </summary>
/// <param name="live">The live profile: each repo it names must have its 'repo.added'.</param>
/// <param name="backfill">Appends a missing 'repo.added' (the re-run rule of AddRepo, setup-once spec §5.1).</param>
/// <param name="mcp">The automatic MCP registration (SO-A4); absent in a host that has none.</param>
internal sealed class SetupService(IProfileSource live, RepoAddedBackfill backfill, McpAutoRegister? mcp = null) : IScheduledService
{
    /// <summary>The service's name in the schedule ('setup'); its actor is 'service:setup'.</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("setup");

    /// <summary>The backstop tick without a profile entry: every 60 seconds.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(60)));

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsSuccess && loaded.Value.Profile.Repos is { Count: > 0 } repos)
        {
            await backfill.Ensure(repos, cancellationToken).ConfigureAwait(false);
        }

        if (mcp is not null)
        {
            await mcp.Run(cancellationToken).ConfigureAwait(false);
        }

        return ScheduledResult.Quiet;
    }
}
