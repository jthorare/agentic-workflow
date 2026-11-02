using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Xunit;

namespace Aw.Context.Testing.Records;

/// <summary>
/// The contract every <see cref="RecordFeed"/> honours (spec §5.2): pages within the limit, a replay from the same cursor
/// answers the same page, paging reaches every item once and then an empty page, and the window's start excludes older
/// records. A binding creates a feed that serves exactly the items it is given.
/// </summary>
/// <typeparam name="TFeed">The implementation.</typeparam>
public abstract class RecordFeedContract<TFeed>
    where TFeed : RecordFeed
{
    private const int Count = 5;

    private static readonly string _owner = typeof(TFeed).Name;

    /// <summary>A fresh feed serving these items, newest first.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The feed.</returns>
    protected abstract TFeed Create(IReadOnlyList<RecordItem> items);

    /// <summary>Paging reaches every item once, then an empty page; a replay repeats its page.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paging_reaches_every_item_and_a_replay_repeats_its_page()
    {
        var feed = Create(Items());
        var seen = new List<RecordKey>();
        SourceCursor? cursor = null;
        for (var pages = 0; pages < Count + 2; pages++)
        {
            var pull = new RecordPull(cursor, 2, NotBefore: null);
            var page = ContractCheck.Ok(_owner, await feed.Pull(pull, CancellationToken.None));
            var replay = ContractCheck.Ok(_owner, await feed.Pull(pull, CancellationToken.None));
            ContractCheck.That(_owner, page.Items.Count <= 2, "a page stays within its limit");
            ContractCheck.That(_owner, page.Items.Select(i => i.Key).SequenceEqual(replay.Items.Select(i => i.Key)) && page.Next == replay.Next, "a replay from the same cursor answers the same page");
            if (page.Items.Count == 0)
            {
                break;
            }

            seen.AddRange(page.Items.Select(i => i.Key));
            cursor = page.Next;
        }

        ContractCheck.That(_owner, seen.Count == Count && seen.Distinct().Take(Count + 1).Count() == Count, "paging reaches every item exactly once");
    }

    /// <summary>A record last updated before the window's start is not returned.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_window_start_excludes_older_records()
    {
        var items = Items();
        var feed = Create(items);
        var notBefore = items[2].UpdatedAt;
        var page = ContractCheck.Ok(_owner, await feed.Pull(new RecordPull(After: null, 100, notBefore), CancellationToken.None));
        ContractCheck.That(_owner, page.Items.Count == 3 && page.Items.All(i => i.UpdatedAt >= notBefore), "only the three records updated on or after the window start are returned");
    }

    private static RecordItem Item(string id, int ageDays) => new(
        RecordKey.From($"linear:issue/{id}"), RecordKind.Issue, $"Title {id}", $"Text {id}", Author: null, Url: null, RecordState.Open, StateRaw: null, CreatedAt: null,
        RecordSamples.Now.AddDays(-ageDays), ClosedAt: null, SourceVersion.From($"v{id}"), Links: [], Repo: null, Audience: null);

    private static List<RecordItem> Items() =>
    [
        .. Enumerable.Range(0, Count).Select(i => Item(i.ToString(CultureInfo.InvariantCulture), i)),
    ];
}
