using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The pure mapping from the library's docs, items and claims to the doc map and test rows of the PR page (spec §4.4, §6.2).</summary>
internal static partial class ReviewPageDocs
{
    private const int MaxTitle = 120;
    private const int MaxDepth = 16;

    internal static ReviewPageClaim Claim(LibraryClaim claim, RecordState? state) => claim.Resolution.Status switch
    {
        LibraryRefStatus.Resolved => new ReviewPageClaim(claim.Resolution.Ref, "resolved", Error: null),
        LibraryRefStatus.Ambiguous => new ReviewPageClaim(claim.Resolution.Ref, "broken", $"{claim.Resolution.Ref} is in two places; write doc:<root>/<doc id>#<item>."),
        LibraryRefStatus.Missing when state == RecordState.Merged => new ReviewPageClaim(claim.Resolution.Ref, "stale", Error: null),
        LibraryRefStatus.Missing => new ReviewPageClaim(claim.Resolution.Ref, "broken", $"{claim.Resolution.Ref}: no readable doc or item has that name."),
        _ => new ReviewPageClaim(claim.Resolution.Ref, "broken", $"{claim.Resolution.Ref}: no readable doc or item has that name."),
    };

    internal static ReviewPageDocFact Doc(LibraryDoc doc, IReadOnlyList<LibraryItem> items, ReviewPageDocClaims claims, LibraryFolders folders)
    {
        var byKey = items.ToDictionary(static i => i.Key, StringComparer.Ordinal);
        var claimedBy = claims.OnDoc
            .SelectMany(c => c.Resolution.Item is { } item && c.Pr != claims.Self && NumberOf(c.Pr) is { } number ? new[] { (Item: item.Key, Number: number, Merged: c.PrState == RecordState.Merged) } : [])
            .Distinct().OrderBy(static c => c.Number).GroupBy(static c => c.Item, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => (IReadOnlyList<ReviewPageClaimedBy>)[.. g.Select(static c => new ReviewPageClaimedBy(c.Number, c.Merged, ClaimedByText(c.Number, c.Merged)))], StringComparer.Ordinal);
        var shown = items.SelectMany(i => KindOf(i.Kind) is { } kind ? new[] { new ReviewPageItemFact(i.Key, FirstLine(i.Text), kind, ShownParent(i, byKey), claims.ClaimedHere.Contains(i.Key), claimedBy.GetValueOrDefault(i.Key, [])) } : []);
        return new ReviewPageDocFact(doc.Slug ?? string.Empty, doc.Title, folders.File(doc.Key.Value), [.. shown]);
    }

    // why: the library stores a TP row's text, not a "verifies" field; a row verifies the items its text names
    internal static ReviewPageTestRow[] TestRows(IReadOnlyList<LibraryItem> items, IReadOnlySet<string> claimed)
    {
        var ids = items.Where(static i => i.Kind != DesignItemKind.TestPlan).Select(static i => i.Key).ToHashSet(StringComparer.Ordinal);
        return [.. items.Where(static i => i.Kind == DesignItemKind.TestPlan)
            .Select(i => (Row: i, Verifies: ItemMention.Matches(i.Text).Select(static m => m.Value).Where(ids.Contains).Distinct(StringComparer.Ordinal).ToList()))
            .Where(r => r.Verifies.Exists(claimed.Contains))
            .Select(r => new ReviewPageTestRow(r.Row.Key, FirstLine(r.Row.Text), r.Verifies, Ticked: false))];
    }

    private static string ClaimedByText(int number, bool merged) => string.Create(CultureInfo.InvariantCulture, $"claimed by #{number}") + (merged ? ", merged" : string.Empty);

    // why: the doc map shows phases, requirements, criteria and tasks; a section between them is not a node, so an item hangs from its nearest shown ancestor
    private static string? ShownParent(LibraryItem item, Dictionary<string, LibraryItem> byKey)
    {
        var parent = item.Parent;
        for (var depth = 0; parent is not null && depth < MaxDepth; depth++)
        {
            if (!byKey.TryGetValue(parent, out var up))
            {
                return null;
            }

            if (KindOf(up.Kind) is not null)
            {
                return up.Key;
            }

            parent = up.Parent;
        }

        return null;
    }

    private static string? KindOf(DesignItemKind kind) => kind switch
    {
        DesignItemKind.Phase => "phase",
        DesignItemKind.Requirement => "req",
        DesignItemKind.Acceptance => "ac",
        DesignItemKind.Task => "task",
        DesignItemKind.Section => null,
        DesignItemKind.TestPlan => null,
        _ => null,
    };

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        return line.Length <= MaxTitle ? line : line[..MaxTitle];
    }

    private static int? NumberOf(RecordKey pr) => int.TryParse(pr.Value[(pr.Value.LastIndexOf('/') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    [GeneratedRegex("[A-Z]{2,5}-[A-Z]?[0-9]+|Ph[0-9]+[a-z]?(?:-[0-9]+)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItemMention { get; }
}
