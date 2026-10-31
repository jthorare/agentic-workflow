namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One page of a <see cref="PagesSetBody"/>.</summary>
/// <param name="Id">The page id; null derives it from the name.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Repo">The repo; null lets the server judge it.</param>
/// <param name="Surface">The map surface it names; null for an owner page.</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PageSetBody(string? Id, string Name, string? Repo, string? Surface, IReadOnlyList<PageActionSetBody> Actions);
