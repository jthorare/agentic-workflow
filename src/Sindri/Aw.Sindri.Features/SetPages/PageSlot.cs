using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SetPages;

/// <summary>A page of the profile's, where it is written (a repo's own 'index.pages', or null for the top-level list) and whether an earlier edit of this request removed it.</summary>
/// <param name="entry">The page as it stands.</param>
/// <param name="repo">The repo whose own list holds it, or null.</param>
internal sealed class PageSlot(PageSettings entry, string? repo)
{
    internal PageSettings Entry { get; set; } = entry;

    internal string? Repo { get; } = repo;

    internal bool Removed { get; set; }

    /// <summary>The id the page has: the one written, else the slug of its name.</summary>
    internal string Key => Entry.Id?.Value ?? PageSlugs.Of(Entry.Name);
}
