using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// The pages of one GitHub repo events read so far (newest first). It keeps the events of the asked kind that are newer than the floor, the newest
/// created_at of ANY kind (the cursor), and whether a page reached back to the floor (so no more pages are needed).
/// </summary>
/// <param name="kind">The asked event kind.</param>
/// <param name="floor">Only events created after this are kept; null for a baseline, which keeps none.</param>
internal sealed class GitHubFeed(EventKind kind, DateTimeOffset? floor)
{
    private readonly List<PolledEvent> _events = [];

    internal IReadOnlyList<PolledEvent> Events => _events;

    internal PollCursorText Newest { get; private set; }

    internal bool ReachedFloor { get; private set; }

    internal Result<Unit> Add(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return SindriErrors.ConnectionProviderChanged.With("the events answer is not a list");
            }

            DateTimeOffset? oldest = null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (Take(item) is not { } at)
                {
                    return SindriErrors.ConnectionProviderChanged.With("an event has no id or created_at");
                }

                oldest = oldest is null || at < oldest ? at : oldest;
            }

            ReachedFloor = oldest is null || floor is not { } line || oldest <= line;   // a baseline (no floor) never needs a second page
            return Unit.Value;
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the events answer is not JSON");
        }
    }

    // Keeps the event when it is of the asked kind and after the floor; returns its time, or null for an event with no id or created_at.
    private DateTimeOffset? Take(JsonElement item)
    {
        var text = PollJson.Text(PollJson.Member(item, "created_at"));
        if (PollJson.Text(PollJson.Member(item, "id")) is null || PollJson.Time(text) is not { } at)
        {
            return null;
        }

        Newest = Newest.Later(text);
        if (floor is { } cut && at > cut && GitHubEventMapper.KindOf(item) == kind && GitHubEventMapper.Map(item, kind) is { } polled)
        {
            _events.Add(polled);
        }

        return at;
    }
}
