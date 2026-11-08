using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ExpireMemory;

/// <summary>The 'memory-expire' built-in service: once an hour it deletes every fact whose expiry has passed. It runs without a profile entry.</summary>
/// <param name="store">The memories.</param>
/// <param name="embedder">Embeds facts that were written while the model was down.</param>
/// <param name="time">The clock.</param>
internal sealed class ExpireMemoryService(IMemoryStore store, IMemoryEmbedder embedder, TimeProvider time) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Deletes memory facts whose expiry has passed.";

    private const int BatchSize = 20;

    private readonly IMemoryStore _store = store;
    private readonly IMemoryEmbedder _embedder = embedder;
    private readonly TimeProvider _time = time;

    /// <summary>Gets the service's name in the schedule ('memory-expire').</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("memory-expire");

    /// <summary>Gets the hourly backstop the scheduler uses without a profile entry.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromHours(1)), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var deleted = await _store.DeleteExpired(_time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (deleted.IsError)
        {
            return deleted.Error;
        }

        var embedded = await ReEmbed(cancellationToken).ConfigureAwait(false);
        return embedded.IsError ? embedded.Error : deleted.Value + embedded.Value > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }

    // Facts written while the model was down have no vector; once it answers they get one, a bounded batch per pass.
    private async ValueTask<Result<int>> ReEmbed(CancellationToken cancellationToken)
    {
        var waiting = await _store.Unembedded(BatchSize, cancellationToken).ConfigureAwait(false);
        if (waiting.IsError || waiting.Value.Count == 0)
        {
            return waiting.IsError ? waiting.Error : 0;
        }

        var answer = await _embedder.Embed([.. waiting.Value.Select(static f => f.Text)], cancellationToken).ConfigureAwait(false);
        if (answer.Vectors is not { } vectors || vectors.Count != waiting.Value.Count)
        {
            return 0;
        }

        var done = 0;
        for (var i = 0; i < vectors.Count; i++)
        {
            var set = await _store.SetEmbedding(waiting.Value[i].Store, waiting.Value[i].Key, vectors[i], answer.Model, cancellationToken).ConfigureAwait(false);
            if (set.IsError)
            {
                return set.Error;
            }

            done += set.Value ? 1 : 0;
        }

        return done;
    }
}
