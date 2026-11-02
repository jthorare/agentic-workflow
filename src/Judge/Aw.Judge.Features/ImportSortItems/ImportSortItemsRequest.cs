namespace Aw.Judge.Features.ImportSortItems;

/// <summary>The ImportSortItems request ('judge prompt-sort import [--since 14d]').</summary>
/// <param name="Since">How far back to look for prompt-sort decisions.</param>
public sealed record ImportSortItemsRequest(TimeSpan Since);
