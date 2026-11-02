namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One page of <see cref="PagesView"/>.</summary>
/// <param name="Id">Its id ('schedule-week').</param>
/// <param name="Key">The surface's node key.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Repo">The repo it is in, or null.</param>
/// <param name="Mark">'owner' or 'suggested'.</param>
/// <param name="Code">True when a code file implements the surface.</param>
/// <param name="Actions">Its actions.</param>
internal sealed record PageView(string Id, string Key, string Name, string? Repo, string Mark, bool Code, IReadOnlyList<PageActionView> Actions);
