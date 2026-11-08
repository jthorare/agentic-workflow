using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>The pages of one Linear 'issues' query read so far: the events of the asked kind newer than the cursor, and the newest time of ANY kind (the next cursor).</summary>
/// <param name="kind">The asked event kind.</param>
/// <param name="after">Only events after this are kept; null for a baseline, which keeps none.</param>
/// <param name="start">The cursor the poll began with.</param>
internal sealed class LinearFeed(EventKind kind, DateTimeOffset? after, string? start)
{
    private readonly List<PolledEvent> _events = [];

    /// <summary>Gets a value indicating whether this poll reads events (a cursor was saved) and is not a baseline.</summary>
    internal bool Reading { get; } = after is not null;

    internal IReadOnlyList<PolledEvent> Events => _events;

    internal PollCursorText Newest { get; private set; } = new PollCursorText(Text: null, At: null).Later(start);

    /// <summary>Gets the endCursor of the last page when Linear said there is another, else null.</summary>
    internal string? More { get; private set; }

    /// <summary>Gets a value indicating whether some issue's comments or history had a next page that was not read (those lists are read 10 and 25 at a time).</summary>
    internal bool NestedMore { get; private set; }

    internal Result<Unit> Add(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var issues = PollJson.Path(document.RootElement, "data", "issues");
            if (PollJson.Member(document.RootElement, "errors") is not null
                || PollJson.Member(issues ?? default, "nodes") is not { ValueKind: JsonValueKind.Array } nodes)
            {
                return SindriErrors.ConnectionProviderChanged.With("the issues answer is not the shape Sindri expects");
            }

            foreach (var issue in nodes.EnumerateArray())
            {
                if (after is not null)
                {
                    _events.AddRange(LinearEventMapper.Map(issue, kind, after));
                }

                Newest = LinearTimes.Newest(issue, Newest);
                NestedMore |= LinearTimes.HasMore(issue);
            }

            var info = PollJson.Member(issues ?? default, "pageInfo") ?? default;
            More = PollJson.Member(info, "hasNextPage") is { ValueKind: JsonValueKind.True } ? PollJson.Text(PollJson.Member(info, "endCursor")) : null;
            return Unit.Value;
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the issues answer is not JSON");
        }
    }
}
