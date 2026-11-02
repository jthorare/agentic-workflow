using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SetPages;

/// <summary>The live profile with the pages the edits left.</summary>
internal static class PlannedProfile
{
    // The profile as it will be written: each list holds its pages as the edits left them.
    internal static SindriProfile Of(SindriProfile profile, List<PageSlot> slots)
    {
        var repos = (profile.Repos ?? []).Select(r => WithPages(r, slots)).ToList();
        return profile with { Index = WithTopPages(profile.Index, slots), Repos = profile.Repos is null ? null : repos };
    }

    private static IndexSettings? WithTopPages(IndexSettings? index, List<PageSlot> slots)
    {
        var top = slots.Where(static s => s.Repo is null && !s.Removed).Select(static s => s.Entry).ToList();
        return index is null && top.Count == 0 ? null : (index ?? new IndexSettings()) with { Pages = top.Count == 0 ? null : top };
    }

    private static RepoEntry WithPages(RepoEntry repo, List<PageSlot> slots)
    {
        var own = slots.Where(s => !s.Removed && string.Equals(s.Repo, repo.Name, StringComparison.Ordinal)).Select(static s => s.Entry).ToList();
        return repo.Index is null && own.Count == 0 ? repo : repo with { Index = (repo.Index ?? new IndexSettings()) with { Pages = own.Count == 0 ? null : own } };
    }
}
