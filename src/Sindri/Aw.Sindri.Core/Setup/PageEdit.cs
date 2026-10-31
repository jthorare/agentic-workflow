using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Setup;

/// <summary>One change <see cref="IProfileIndexWriter"/> makes to an 'index.pages' list (pages spec §3.1).</summary>
/// <param name="Id">The page's id.</param>
/// <param name="Entry">The whole page to set (its 'id' is <paramref name="Id"/>), or null to remove the page.</param>
/// <param name="Repo">The repo whose own 'repos.&lt;name&gt;.index.pages' holds the page, or null for the profile's top-level 'index.pages'.</param>
public sealed record PageEdit(PagePinId Id, PageSettings? Entry, string? Repo = null) : IndexEdit;
