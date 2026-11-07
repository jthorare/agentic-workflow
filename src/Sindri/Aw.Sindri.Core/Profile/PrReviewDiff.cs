namespace Aw.Sindri.Core.Profile;

/// <summary>The 'review:' key paths a profile sets, for 'profile approve' to compare (PR review page spec §7). An absent key has no path, so adding one shows as a change.</summary>
internal static class PrReviewDiff
{
    internal static IEnumerable<(string Key, string Value)> Keys(PrReviewSettings? review)
    {
        if (review is null)
        {
            yield break;
        }

        if (review.Login is { } login)
        {
            yield return ("review.login", login);
        }

        if (review.Credential is { } credential)
        {
            yield return ("review.credential", credential);
        }

        foreach (var (name, entry) in review.Settings ?? new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal))
        {
            foreach (var (key, value) in Entry($"review.settings.{name}", entry))
            {
                yield return (key, value);
            }
        }

        foreach (var project in review.Projects ?? [])
        {
            yield return ($"review.projects.{project.Project}", $"{string.Join(',', project.Repos ?? [])}|{project.Settings}");
        }
    }

    private static IEnumerable<(string Key, string Value)> Entry(string path, PrReviewSettingsEntry entry)
    {
        if (entry.Extends is { } extends)
        {
            yield return ($"{path}.extends", extends);
        }

        if (entry.Panels is { } panels)
        {
            yield return ($"{path}.panels", string.Join(',', panels));
        }

        if (entry.Groups is { } groups)
        {
            yield return ($"{path}.groups", string.Join(';', groups.Select(static g => $"{g.Name}={string.Join(',', g.Globs)}")));
        }

        if (entry.Collapse is { } collapse)
        {
            yield return ($"{path}.collapse", string.Join(',', collapse));
        }

        if (entry.Risky is { } risky)
        {
            yield return ($"{path}.risky", string.Join(';', risky.Select(static r => $"{string.Join(',', r.Globs)}={r.Why}")));
        }

        if (entry.Checks is { } checks)
        {
            yield return ($"{path}.checks", string.Join(';', checks.Select(static c => $"{c.Key}|{c.Title}|{(c.When is null ? "*" : string.Join(',', c.When))}|{Need(c.Need)}")));
        }
    }

    private static string Need(PrReviewNeed? need) =>
        need is null ? string.Empty : $"{need.Ci}/{need.App}/{string.Join(',', need.ChangedWith ?? [])}/{need.Covers}/{need.Body}/{need.Manual}/{need.Command}";
}
