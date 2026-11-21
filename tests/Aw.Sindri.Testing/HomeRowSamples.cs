using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Testing;

/// <summary>Home rows with deterministic defaults: an open needs-you review request (home spec §3.1).</summary>
public static class HomeRowSamples
{
    /// <summary>The instant every sample row started to need the owner.</summary>
    public static DateTimeOffset Since { get; } = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A row with version 1, three facts and one action.</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="position">Its rank position.</param>
    /// <param name="state">Its state.</param>
    /// <param name="group">Its group.</param>
    /// <returns>The row.</returns>
    public static HomeRow Row(string id = "gh:pr:jthorare/agentic-workflow#3120", int position = 0, HomeState state = HomeState.Open, HomeGroup group = HomeGroup.NeedsYou) =>
        new(
            HomeRowId.From(id),
            Version: 1,
            HomeKind.ReviewRequested,
            group,
            "Review requested: the Home inbox spec",
            [new HomeFact(HomeFactKind.Why, "review requested"), new HomeFact(HomeFactKind.Object, "#3120"), new HomeFact(HomeFactKind.From, "sam")],
            WaitsOn: "you",
            Since,
            new HomeRank(Blocks: 2, Weight: 5, SinceRank: 1, position),
            [new HomeBlocked("gh:pr:jthorare/agentic-workflow#3121", "Stacked PR", "stacked")],
            [new HomeLink("pr", "gh:pr:jthorare/agentic-workflow#3120", Title: null)],
            state,
            SnoozeUntil: null,
            DoneAt: null,
            ["review"],
            new HomeSource("github", Since, Connection: null));
}
