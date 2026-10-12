namespace Aw.Sindri.Core.Profile;

/// <summary>Which settings changed between two profiles, as key paths, for 'profile approve' to show before it records anything.</summary>
public static class ProfileDiff
{
    /// <summary>The key paths whose values differ; every path when there is no earlier approval.</summary>
    /// <param name="approved">The latest approved profile, if any.</param>
    /// <param name="live">The live profile.</param>
    /// <returns>The changed key paths, sorted.</returns>
    public static IReadOnlyList<string> Between(SindriProfile? approved, SindriProfile live)
    {
        var before = approved is null ? new Dictionary<string, string>(StringComparer.Ordinal) : Flatten(approved);
        var after = Flatten(live);
        return [.. before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(k => !before.TryGetValue(k, out var b) || !after.TryGetValue(k, out var a) || !string.Equals(a, b, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    private static Dictionary<string, string> Flatten(SindriProfile p)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = p.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["user"] = p.User,
            ["tenant"] = p.Tenant,
            ["api.port"] = (p.Api ?? new ApiSettings()).Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var repo in p.Repos ?? [])
        {
            values[$"repos.{repo.Name}"] = repo.Path;
        }

        foreach (var (name, settings) in p.Services ?? SindriProfile.NoServices)
        {
            values[$"services.{name}.every"] = settings.Every;
        }

        return values;
    }
}
