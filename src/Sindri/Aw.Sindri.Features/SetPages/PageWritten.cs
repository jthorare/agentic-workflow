using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SetPages;

/// <summary>One page a SetPages job touched.</summary>
/// <param name="Id">The page's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Repo">The repo whose 'index.pages' holds it; null for the top-level list.</param>
/// <param name="Action">'created', 'updated' or 'removed'.</param>
public sealed record PageWritten(PagePinId Id, string Name, string? Repo, string Action);
