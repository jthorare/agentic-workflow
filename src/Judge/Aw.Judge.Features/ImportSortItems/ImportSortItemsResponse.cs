namespace Aw.Judge.Features.ImportSortItems;

/// <summary>The ImportSortItems response.</summary>
/// <param name="Imported">How many new sort eval items were added.</param>
/// <param name="Skipped">How many decisions were already imported or had no usable axes.</param>
public sealed record ImportSortItemsResponse(int Imported, int Skipped);
