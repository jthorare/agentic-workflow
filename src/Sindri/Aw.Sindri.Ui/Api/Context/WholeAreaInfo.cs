namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The area an answer was opened on (area=).</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Truncated">True when the area was cut at the cap.</param>
/// <param name="Counts">The area's full counts.</param>
/// <param name="Page">The page this answer holds (1-based).</param>
/// <param name="Pages">How many pages the area has.</param>
internal sealed record WholeAreaInfo(string Key, bool Truncated, WholeCounts Counts, int Page = 1, int Pages = 1);
