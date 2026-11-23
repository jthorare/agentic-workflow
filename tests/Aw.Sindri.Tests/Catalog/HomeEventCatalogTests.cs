using System.Reflection;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The fourteen 'home.*' event types (home spec §3.1, §8) in the event catalog.</summary>
public sealed class HomeEventCatalogTests
{
    private static readonly string[] _types =
    [
        "home.row.raised", "home.row.changed", "home.row.cleared", "home.row.seen", "home.row.snoozed", "home.row.woke", "home.row.done", "home.row.revived", "home.row.muted",
        "home.act.sent", "home.act.landed", "home.act.failed", "home.brief.written", "home.cursor.moved",
    ];

    /// <summary>The fourteen type names.</summary>
    public static TheoryData<string> Types => [.. _types];

    [Fact, Trait("Lane", "unit")]
    public void All_fourteen_home_events_are_registered_once()
    {
        var home = EventCatalog.All.Where(static e => e.Type.StartsWith("home.", StringComparison.Ordinal)).Select(static e => e.Type).ToList();
        var constants = typeof(EventTypes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.IsLiteral && f.FieldType == typeof(string)).Select(static f => (string)f.GetRawConstantValue()!)
            .Where(static t => t.StartsWith("home.", StringComparison.Ordinal));

        Assert.Equal(_types.Order(StringComparer.Ordinal), home.Order(StringComparer.Ordinal));
        Assert.Equal(_types.Order(StringComparer.Ordinal), constants.Order(StringComparer.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Types))]
    public void Home_events_are_public_or_fenced(string type)
    {
        var entry = Assert.Single(EventCatalog.All, e => e.Type == type);

        Assert.True(entry.Public);   // the app re-reads Home on these; the public form carries the row id only, titles, facts and brief lines stay in the ledger
        Assert.Equal(typeof(HomeEventPublic), entry.PublicPayload);
        Assert.Contains(ApiViews.Home, entry.Invalidates, StringComparer.Ordinal);
        Assert.StartsWith(type + ":", entry.KeyShape, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Home_is_an_api_view() => Assert.Contains(ApiViews.Home, ApiViews.All, StringComparer.Ordinal);
}
