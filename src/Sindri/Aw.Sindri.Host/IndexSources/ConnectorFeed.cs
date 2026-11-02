using Aw.Connectors.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using ContextCursor = Aw.Context.Core.Sources.SourceCursor;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// The Host's adapter from a connector to Context's record feed (owned-index spec §5.2: the only place Sindri touches Aw.Connectors.Core). It maps a page of <see cref="ConnectorItem"/> onto <see cref="RecordItem"/>: the key, kind,
/// state and version into Context's types, the connector's 'owner/name' into the profile repo and the audience 'repo:&lt;owner&gt;/&lt;name&gt;', and a 'touches' link's 'path:&lt;owner&gt;/&lt;name&gt;/&lt;path&gt;' into 'path:&lt;profile repo&gt;/&lt;path&gt;'.
/// A record of a repo the profile doesn't match is dropped and counted (<see cref="SkippedNotInProfile"/>), as is a record Context's key rules refuse.
/// </summary>
/// <param name="source">The source this feed reads ('github').</param>
/// <param name="connector">The connector it pulls.</param>
/// <param name="place">The source's own mapping from a record to its repo and audience; null skips the record (default deny).</param>
/// <param name="scrubbers">Cleans every title and text before the record leaves the feed; null for none.</param>
internal sealed class ConnectorFeed(SourceId source, IConnector connector, Func<ConnectorItem, Placement?> place, Scrubbers? scrubbers = null) : RecordFeed(source)
{
    private int _skipped;

    /// <summary>How many records this feed dropped because their repo isn't in the profile.</summary>
    internal int SkippedNotInProfile => Volatile.Read(ref _skipped);

    /// <inheritdoc />
    public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var after = pull.After is { } cursor ? new Aw.Connectors.Core.SourceCursor(cursor.Value) : null;
        var read = await connector.Pull(after, pull.Limit, pull.Before, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var page = read.Value;
        var items = Items(page, pull.NotBefore, pull.Before);
        var removed = page.Removed.Select(static key => RecordKey.TryFrom(key, out var k) ? (RecordKey?)k : null).OfType<RecordKey>().ToList();
        return ContextCursor.TryFrom(page.Next.Value, out var next)
            ? new RecordPage(items, removed, next, page.CursorReset)
            : ConnectorsErrors.Invalid.With("the connector's cursor is longer than Context keeps (1024 characters); list fewer repos");
    }

    // The page's records inside the window and the profile, mapped; the rest are counted or dropped.
    private List<RecordItem> Items(ConnectorPage page, DateTimeOffset? notBefore, DateTimeOffset? before)
    {
        var items = new List<RecordItem>(page.Items.Count);
        foreach (var item in page.Items.Where(i => (notBefore is not { } start || i.UpdatedAt >= start) && (before is not { } end || i.UpdatedAt < end)))
        {
            if (place(item) is not { } placement)
            {
                _ = Interlocked.Increment(ref _skipped);
            }
            else if (Map(Scrubbed(item), placement) is { } mapped)
            {
                items.Add(mapped);
            }
        }

        return items;
    }

    private static RecordItem? Map(ConnectorItem item, Placement placement)
    {
        if (!RecordKey.TryFrom(item.Key, out var key) || !SourceVersion.TryFrom(item.Version, out var version) || Kind(item.Kind) is not { } kind)
        {
            return null;
        }

        var links = item.Links.Select(link => Link(link, placement)).OfType<RecordLink>().ToList();
        return new RecordItem(key, kind, item.Title, item.Text, item.Author, item.Url, State(item.State), item.StateRaw, item.CreatedAt, item.UpdatedAt, item.ClosedAt, version, links, placement.Repo, placement.Audience);
    }

    // A blocked field keeps its raw text on purpose: the sync driver cleans again and must see the block.
    private ConnectorItem Scrubbed(ConnectorItem item)
    {
        if (scrubbers is null)
        {
            return item;
        }

        var text = scrubbers.Ingest.Clean(item.Text, singleLine: false);
        var title = scrubbers.Ingest.Clean(item.Title, singleLine: true);
        return text.Blocked || title.Blocked ? item : item with { Text = text.Text, Title = title.Text };
    }

    private static RecordKind? Kind(string kind) => kind switch
    {
        "issue" => RecordKind.Issue,
        "pr" => RecordKind.Pr,
        "message" => RecordKind.Message,
        "comment" => RecordKind.Comment,
        _ => null,
    };

    private static RecordState? State(string state) => state switch
    {
        "open" => RecordState.Open,
        "closed" => RecordState.Closed,
        "merged" => RecordState.Merged,
        "draft" => RecordState.Draft,
        _ => null,
    };

    private static RecordLink? Link(ConnectorLink link, Placement placement)
    {
        var kind = link.Kind switch
        {
            "closes" => RecordLinkKind.Closes,
            "relates" => RecordLinkKind.Relates,
            "touches" => RecordLinkKind.Touches,
            _ => (RecordLinkKind?)null,
        };
        if (kind is not { } known)
        {
            return null;
        }

        var own = placement.SourceRepo is { } repo ? "path:" + repo + "/" : null;
        return known != RecordLinkKind.Touches
            ? new RecordLink(link.Key, known, Title: null)
            : own is not null && placement.Repo is { } profileRepo && link.Key.StartsWith(own, StringComparison.OrdinalIgnoreCase)
                ? new RecordLink($"path:{profileRepo.Value}/{link.Key[own.Length..]}", known, Title: null)
                : null;
    }
}
