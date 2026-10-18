using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// An agent's spend today (screens spec E-28): what the usage reader the catalog entry names counts for the local calendar day, priced by the table. An agent
/// whose entry names no reader has none (null: a budget on it is refused as 'no usage reader'). A reader nobody has checked against the real CLI counts tokens only.
/// The day is read at most once every few seconds, so a screen that polls does not walk the transcripts each time.
/// </summary>
/// <param name="catalog">The catalog, which names each entry's reader.</param>
/// <param name="readers">The readers by name.</param>
/// <param name="clock">The time and the local zone.</param>
internal sealed class UsageSpend(IAgentCatalog catalog, IReadOnlyDictionary<string, IUsageReader> readers, TimeProvider clock) : ILaunchSpend, ILaunchUsage
{
    private static readonly TimeSpan _fresh = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<AgentId, (DateTimeOffset At, DateOnly Day, AgentSpend Spend)> _read = new();

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId session, CancellationToken cancellationToken) =>
        catalog.Resolve(target)?.Usage is { } name && readers.TryGetValue(name, out var reader) ? await reader.OfSession(session, cancellationToken).ConfigureAwait(false) : null;

    /// <inheritdoc/>
    public async ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken)
    {
        if (catalog.Resolve(target)?.Usage is not { } name || !readers.TryGetValue(name, out var reader))
        {
            return Results.Success(default(AgentSpend));
        }

        var now = clock.GetUtcNow();
        var (start, end, day) = LocalDay(now, clock.LocalTimeZone);
        if (_read.TryGetValue(target, out var cached) && cached.Day == day && now - cached.At < _fresh)
        {
            return Results.Success<AgentSpend?>(cached.Spend);
        }

        var used = Budgets.Spend(await reader.Between(start, end, cancellationToken).ConfigureAwait(false));
        var spend = reader.Verified ? used : new AgentSpend(0m, used.Tokens, SpendBasis.TokensOnly);
        _read[target] = (now, day, spend);
        return Results.Success<AgentSpend?>(spend);
    }

    /// <summary>The local calendar day that holds <paramref name="now"/>, as the instants it starts and ends.</summary>
    private static (DateTimeOffset Start, DateTimeOffset End, DateOnly Day) LocalDay(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        return (Midnight(local.Date, zone), Midnight(local.Date.AddDays(1), zone), DateOnly.FromDateTime(local.Date));
    }

    private static DateTimeOffset Midnight(DateTime date, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.IsInvalidTime(unspecified) ? zone.GetUtcOffset(unspecified.AddHours(1)) : zone.GetUtcOffset(unspecified));
    }
}
