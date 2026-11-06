using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The Reviews page (<see cref="ReviewsPageViewModel"/>, continued): the groups, sections and rows, kept across reads.</summary>
internal sealed partial class ReviewsPageViewModel
{
    private Dictionary<string, ReviewGroupViewModel> _groups = new(StringComparer.Ordinal);
    private Dictionary<string, ReviewSectionViewModel> _sections = new(StringComparer.Ordinal);
    private Dictionary<string, ReviewRowViewModel> _rows = new(StringComparer.Ordinal);

    // The groups, sections and rows as served, reusing the ones already shown (by kind, section name and PR ref) so a refresh leaves focus where it was.
    private List<ReviewGroupViewModel> ShowGroups(ReviewList list, DateTimeOffset now)
    {
        var groups = new Dictionary<string, ReviewGroupViewModel>(StringComparer.Ordinal);
        var sections = new Dictionary<string, ReviewSectionViewModel>(StringComparer.Ordinal);
        var rows = new Dictionary<string, ReviewRowViewModel>(StringComparer.Ordinal);
        var shown = new List<ReviewGroupViewModel>();
        foreach (var served in list.Groups.Where(g => g.Sections.Any(s => s.Rows.Count > 0)))
        {
            var group = _groups.TryGetValue(served.Kind, out var kept) && string.Equals(kept.Title, served.Title, StringComparison.Ordinal) ? kept : new ReviewGroupViewModel(served.Kind, served.Title);
            var shownSections = new List<ReviewSectionViewModel>();
            foreach (var servedSection in served.Sections.Where(s => s.Rows.Count > 0))
            {
                var key = served.Kind + "/" + servedSection.By + "/" + servedSection.Name;
                var section = _sections.TryGetValue(key, out var keptSection) ? keptSection : new ReviewSectionViewModel(servedSection);
                Patch.To(section.Rows, [.. servedSection.Rows.Select(r => Row(r, list.Login, now, key, rows))]);
                sections[key] = section;
                shownSections.Add(section);
            }

            Patch.To(group.Sections, shownSections);
            group.Recount();
            groups[served.Kind] = group;
            shown.Add(group);
        }

        (_groups, _sections, _rows) = (groups, sections, rows);
        return shown;
    }

    private ReviewRowViewModel Row(ReviewRowView served, string? login, DateTimeOffset now, string section, Dictionary<string, ReviewRowViewModel> shown)
    {
        var key = section + "/" + served.Repo + "#" + served.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_rows.TryGetValue(key, out var kept))
        {
            kept.Update(served, login, now);
        }
        else
        {
            kept = new ReviewRowViewModel(served, login, now);
        }

        shown[key] = kept;
        return kept;
    }
}
