namespace Aw.Sindri.Features.Build;

/// <summary>One acceptance item and the tests written for it.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Tests">The test ids, appended to the red command.</param>
internal sealed record RedItem(string Id, IReadOnlyList<string> Tests);
