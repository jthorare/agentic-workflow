namespace Aw.Sindri.Ui.Api.Home;

/// <summary>One object that needs, or may soon need, the owner (home spec §3.1).</summary>
/// <param name="Id">'&lt;source&gt;:&lt;kind&gt;:&lt;source id&gt;'.</param>
/// <param name="Version">Starts at 1 and moves by 1 per change.</param>
/// <param name="Kind">The row's kind, such as 'review-requested'.</param>
/// <param name="Group">'needs-you' or 'moving'.</param>
/// <param name="Title">The owner's words for it.</param>
/// <param name="Facts">Up to three facts, most important first.</param>
/// <param name="WaitsOn">Who has the next move.</param>
/// <param name="Since">When it started to need the owner.</param>
/// <param name="Rank">The rank record.</param>
/// <param name="BlocksList">The objects behind the blocks count, at most 20.</param>
/// <param name="Links">Cross-links to other objects.</param>
/// <param name="State">'open', 'snoozed', 'done' or 'muted'.</param>
/// <param name="SnoozeUntil">When a snoozed row wakes, or null.</param>
/// <param name="DoneAt">When a done row was marked done, or null.</param>
/// <param name="Actions">Action ids that apply; the first is the primary.</param>
/// <param name="Source">Where the facts came from.</param>
internal sealed record HomeRowView(
    string Id,
    long Version,
    string Kind,
    string Group,
    string Title,
    IReadOnlyList<HomeFactView> Facts,
    string WaitsOn,
    DateTimeOffset Since,
    HomeRankView Rank,
    IReadOnlyList<HomeBlockedView> BlocksList,
    IReadOnlyList<HomeLinkView> Links,
    string State,
    DateTimeOffset? SnoozeUntil,
    DateTimeOffset? DoneAt,
    IReadOnlyList<string> Actions,
    HomeSourceView Source);
