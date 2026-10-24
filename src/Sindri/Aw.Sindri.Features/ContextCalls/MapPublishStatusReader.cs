using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Reads the stored publish state for the caller's repos, the index's commits and the ledger's last 'published' and 'promoted' events, and puts
/// them through <see cref="MapPublishStatusRules"/>. The one reader behind 'context map status', the Health card and doctor's 'context:publish'.
/// A session's repos are cut by the engine (its PublishStatus call), so nothing here sees another repo.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="ledger">The ledger, for how the current map was published.</param>
/// <param name="clock">The time.</param>
internal sealed class MapPublishStatusReader(IContextEngine engine, IIndexOutlooks outlooks, ILedgerReader ledger, TimeProvider clock)
{
    /// <summary>The status as the caller of <paramref name="scope"/> may see it.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The status, SND-CONTEXT-030 when there is neither a map nor a hold, or the engine's refusal.</returns>
    internal async ValueTask<Result<MapPublishStatusView>> Read(ContextScope scope, CancellationToken cancellationToken)
    {
        var view = await engine.PublishStatus(scope, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        if (view.Value.Current is null && view.Value.Hold is null)
        {
            return ContextErrors.NoMap.With("there is no map yet, so nothing has been published (fix: sindri context map import <file>, or sindri context map build)");
        }

        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var facts = view.Value.Hold is { } hold
            ? await Held(view.Value, hold, settings.Value, cancellationToken).ConfigureAwait(false)
            : await Current(view.Value, cancellationToken).ConfigureAwait(false);
        return facts.IsError ? facts.Error : MapPublishStatusRules.Of(facts.Value, clock.GetUtcNow(), clock.LocalTimeZone);
    }

    private async ValueTask<Result<MapPublishFacts>> Held(MapPublishView view, MapHold hold, ContextSettings settings, CancellationToken cancellationToken)
    {
        var state = new MapHoldState(hold.Version, hold.Since, hold.Heads.ToDictionary(static h => h.Repo.Value, static h => new MapRepoHead(h.Commit, h.OnDefault), StringComparer.Ordinal));
        var read = await outlooks.Read(state, cancellationToken).ConfigureAwait(false);
        var commits = await outlooks.IndexCommits([.. hold.Heads.Select(static h => h.Repo.Value)], cancellationToken).ConfigureAwait(false);
        if (read.IsError || commits.IsError)
        {
            return read.IsError ? read.Error : commits.Error;
        }

        var repos = hold.Heads.Select(h =>
        {
            var outlook = read.Value.TryGetValue(h.Repo.Value, out var detail) ? detail : null;
            var waiting = outlook is { Outlook: IndexOutlook.Coming or IndexOutlook.Backoff };
            return new PublishRepoFact(h.Repo.Value, h.Commit, commits.Value.GetValueOrDefault(h.Repo.Value), waiting, outlook?.Word ?? RepoWait.None, outlook?.RetryAt);
        });
        return new MapPublishFacts(hold.Version, hold.Since, TimeSpan.FromMinutes(settings.Publish.WaitForIndexMinutes), view.Current, [.. repos], Reason: null, ByHand: false);
    }

    private async ValueTask<Result<MapPublishFacts>> Current(MapPublishView view, CancellationToken cancellationToken)
    {
        var commits = await outlooks.IndexCommits([.. view.CurrentHeads.Select(static h => h.Repo.Value)], cancellationToken).ConfigureAwait(false);
        var window = await MapEvents(cancellationToken).ConfigureAwait(false);
        if (commits.IsError || window.IsError)
        {
            return commits.IsError ? commits.Error : window.Error;
        }

        var repos = view.CurrentHeads.Select(h => new PublishRepoFact(h.Repo.Value, h.Commit, commits.Value.GetValueOrDefault(h.Repo.Value), Waiting: false, RepoWait.None));
        var names = view.Repos.Select(static r => r.Value).ToHashSet(StringComparer.Ordinal);
        return Settled(view.Current, [.. repos], [.. window.Value.Where(e => BelongsTo(e, names))]);
    }

    private static MapPublishFacts Settled(int? current, IReadOnlyList<PublishRepoFact> repos, List<LedgerEvent> mine)
    {
        var published = Newest(mine, EventTypes.ContextMapPublished, current);
        var promoted = Newest(mine, EventTypes.ContextMapPromoted, current);
        var reason = published is null ? null : Text(Payload(published), "reason");
        var byHand = promoted is not null && promoted.Envelope.Actor.StartsWith("human:", StringComparison.Ordinal);
        return new MapPublishFacts(HeldVersion: null, default, TimeSpan.Zero, current, repos, reason, byHand, published?.Envelope.OccurredAt, DroppedHold(mine));
    }

    // The held map a newer build replaced, when that drop is the newest map event.
    private static int? DroppedHold(List<LedgerEvent> mine) =>
        mine.MaxBy(static e => e.Sequence) is { } newest && newest.Envelope.Type == EventTypes.ContextMapHoldDropped && Payload(newest).TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : null;

    // The map events of the ledger's recent tail, oldest first: the latest of a type may belong to another repo, so the reader looks through a bounded window.
    private async ValueTask<Result<List<LedgerEvent>>> MapEvents(CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        List<LedgerEvent> found = [];
        var cursor = Math.Max(0, head.Value - TailWindow);
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            if (page.Value.Count == 0)
            {
                return found;
            }

            found.AddRange(page.Value.Where(static e => _mapTypes.Contains(e.Envelope.Type)));
            cursor = page.Value[^1].Sequence;
        }
    }

    private const int TailWindow = 100_000;
    private const int PageSize = 500;

    private static readonly HashSet<string> _mapTypes = new(StringComparer.Ordinal)
    {
        EventTypes.ContextMapPublished, EventTypes.ContextMapPromoted, EventTypes.ContextMapHeld, EventTypes.ContextMapHoldDropped, EventTypes.ContextMapImported,
    };

    private static LedgerEvent? Newest(List<LedgerEvent> events, string type, int? version) =>
        version is null ? null : events.Where(e => e.Envelope.Type == type && Payload(e).TryGetProperty("version", out var n) && n.TryGetInt32(out var number) && number == version).MaxBy(static e => e.Sequence);

    // True when the event's repo is one the caller sees (an event without a repo is everyone's); the ledger holds every repo's events.
    private static bool BelongsTo(LedgerEvent recorded, HashSet<string> repos) =>
        !Payload(recorded).TryGetProperty("repo", out var repo) || repo.ValueKind != JsonValueKind.String || repos.Contains(repo.GetString() ?? string.Empty);

    private static JsonElement Payload(LedgerEvent recorded)
    {
        using var document = JsonDocument.Parse(recorded.Envelope.Payload);
        return document.RootElement.Clone();
    }

    private static string? Text(JsonElement payload, string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
