using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The Linear project feed (projects page spec §4.1): the projects of the profile's teams, newest first, from the same <see cref="FeedCursor"/> as the issue feed. Projects have no window (their age says
/// nothing about whether they are live), so Recent returns every project in scope, Backfill an empty page at once (Recent already read everything), and Incremental the projects updated after the watermark.
/// </summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long each call gets.</param>
internal sealed class LinearProjectFeed(LinearClient client, TimeSpan deadline) : RecordFeed(LinearSourceIds.Projects)
{
    private const int MaxPage = 50;

    /// <inheritdoc />
    public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        if (pull.Before is not null)
        {
            return new RecordPage([], [], pull.After ?? FeedCursor.Empty.ToCursor(), CursorReset: false);   // Backfill: the window is the whole set, which Recent read
        }

        var at = FeedCursor.Read(pull.After);
        var variables = LinearFilters.Variables(Filter(at.Since), Math.Clamp(pull.Limit, 1, MaxPage), at.After);
        var data = await client.Query<LinearProjectsPullData>("ProjectsPull", variables, deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        var connection = data.Value.Projects;
        var items = connection.Nodes.Select(n => LinearProjectItem.Of(n, client.Settings.Teams)).ToList();
        var top = items.Count == 0 ? at.Top : items.Max(static i => i.UpdatedAt) is { } newest && newest > (at.Top ?? DateTimeOffset.MinValue) ? newest : at.Top;
        var next = connection.PageInfo is { HasNextPage: true, EndCursor: { } end } ? new FeedCursor(at.Since, end, top) : new FeedCursor(top ?? at.Since, After: null, Top: null);
        return new RecordPage(items, [], next.ToCursor(), CursorReset: false);
    }

    private JsonObject Filter(DateTimeOffset? since)
    {
        var filter = client.Settings.Teams.Count > 0 ? LinearFilters.OfTeams([], client.Settings.Teams) : [];
        if (since is { } after)
        {
            filter["updatedAt"] = new JsonObject { ["gt"] = after.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) };
        }

        return filter;
    }
}
