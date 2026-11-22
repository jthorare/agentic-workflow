using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.HomeRoutes;

/// <summary>The Home rows the tests read: the §4.2 example of the Home spec from tests/fixtures/home/rows.json, and hand-made rows of a chosen id, group, kind and position.</summary>
internal static class HomeFixture
{
    public const string Meta = "lane:recycle:meta";
    public const string Review = "gh:review:jthorare/agentic-workflow#3120";
    public const string Ready3099 = "gh:ready:jthorare/agentic-workflow#3099";
    public const string Ideas = "lane:recycle:ideas";
    public const string Ready3101 = "gh:ready:jthorare/agentic-workflow#3101";
    public const string Dm = "slack:dm:sam-0902";

    private static readonly DateTimeOffset _since = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The six rows of the spec's example, in the order the file lists them (their rank positions are 1 to 6).</summary>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<HomeRow> Rows()
    {
        var path = Path.Combine(SessionFixtures.Root, "tests", "fixtures", "home", "rows.json");
        var options = SliceHarness.Json;
        var info = (JsonTypeInfo<HomeRow>)options.GetTypeInfo(typeof(HomeRow));
        return [.. JsonNode.Parse(File.ReadAllText(path))!["rows"]!.AsArray().Select(n => n.Deserialize(info)!)];
    }

    /// <summary>A view holding the six example rows.</summary>
    /// <returns>The view.</returns>
    public static InMemoryHomeView View()
    {
        var view = new InMemoryHomeView();
        foreach (var row in Rows())
        {
            view.Put(row);
        }

        return view;
    }

    /// <summary>One open row with the given id, ranked at <paramref name="position"/>.</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="position">Its place in the order.</param>
    /// <param name="group">Its group.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="state">Its state.</param>
    /// <returns>The row.</returns>
    public static HomeRow Row(string id, int position, HomeGroup group = HomeGroup.NeedsYou, HomeKind kind = HomeKind.ReviewRequested, HomeState state = HomeState.Open) =>
        new(HomeRowId.From(id), 1, kind, group, $"Row {position}", [new HomeFact(HomeFactKind.Why, "why"), new HomeFact(HomeFactKind.Ref, id), new HomeFact(HomeFactKind.From, "Sam")], "you", _since.AddMinutes(position),
            new HomeRank(0, 5, position, position), [new HomeBlocked(HomeRowId.From("gh:pr:a/b#1"), "PR 1", "closes")], [new HomeLink("pr", HomeRowId.From("gh:pr:a/b#1"), "PR 1")], state,
            state == HomeState.Snoozed ? _since.AddDays(1) : null, state == HomeState.Done ? _since.AddHours(1) : null, ["open"], new HomeSource("github", _since, null));
}
