namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One item of the doc map (a phase, a requirement): who claims it and whether Send as fix task is offered.</summary>
/// <param name="Id">The item id (Ph5, REQ-E3).</param>
/// <param name="Title">Its title.</param>
/// <param name="Kind">phase or req.</param>
/// <param name="ClaimedHere">True when this PR claims it.</param>
/// <param name="ClaimedBy">Other PRs that claim it.</param>
/// <param name="Unclaimed">True for a requirement no PR claims.</param>
/// <param name="FixTask">True when Send as fix task is offered.</param>
/// <param name="Items">Its children.</param>
internal sealed record ReviewDocItemView(
    string Id,
    string Title,
    string Kind,
    bool ClaimedHere,
    IReadOnlyList<ReviewClaimedByView> ClaimedBy,
    bool Unclaimed,
    bool FixTask,
    IReadOnlyList<ReviewDocItemView> Items);
