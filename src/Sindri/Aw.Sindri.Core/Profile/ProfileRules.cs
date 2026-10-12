using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks the JSON shape can't express: version, required text, service names and cadences.</summary>
public static class ProfileRules
{
    /// <summary>Checks <paramref name="profile"/> and returns it with defaults applied (sorted services and repos).</summary>
    /// <param name="profile">The deserialized profile.</param>
    /// <returns>The canonical profile, or SND-PROFILE-002 naming the key path and the fix.</returns>
    public static Result<SindriProfile> Check(SindriProfile profile)
    {
        if (profile.SchemaVersion != SindriProfile.CurrentSchemaVersion)
        {
            return Invalid("schemaVersion", string.Create(CultureInfo.InvariantCulture,
                $"is {profile.SchemaVersion}; this build reads {SindriProfile.CurrentSchemaVersion}"));
        }

        if (string.IsNullOrWhiteSpace(profile.User) || string.IsNullOrWhiteSpace(profile.Tenant))
        {
            return Invalid(string.IsNullOrWhiteSpace(profile.User) ? "user" : "tenant", "must be non-empty text");
        }

        if (profile.Api is { Port: < 0 or (> 0 and < 1024) or > 65535 })
        {
            return Invalid("api.port", "must be 0 (a free port) or 1024 to 65535");
        }

        foreach (var (name, settings) in profile.Services ?? SindriProfile.NoServices)
        {
            if (!ServiceName.TryFrom(name, out _))
            {
                return Invalid($"services.{name}", "is not a service name: kebab-case, 1 to 40 characters");
            }

            if (Cadence.Parse(settings.Every) is { IsSuccess: false } bad)
            {
                return Invalid($"services.{name}.every", bad.Error.Message);
            }
        }

        return Canonical(profile);
    }

    /// <summary>
    /// Whether the profile with hash <paramref name="live"/> is approved: it is the saved approval and the latest recorded
    /// 'profile.approved' event names it. A saved approval whose event is missing or isn't the latest (its save failed after
    /// a later approval was recorded) doesn't count, so approving again records it. Every surface judges by this one rule:
    /// profile validate, approve's preview, the status ('needsApproval') and the startup summary, and doctor's profile check.
    /// </summary>
    /// <param name="saved">The latest saved approval's hash, or null.</param>
    /// <param name="recorded">The hash the latest recorded approval names, or null.</param>
    /// <param name="live">The hash judged.</param>
    /// <returns>True when approved.</returns>
    public static bool IsApproved(ProfileHash? saved, ProfileHash? recorded, ProfileHash live) => saved == live && recorded == live;

    /// <summary>
    /// Whether the profile needs a (new) approval: the live profile isn't approved (<see cref="IsApproved"/>). A live profile
    /// that doesn't read is judged by the saved approval instead (its error is reported on its own), so it needs approval
    /// only when nothing is saved or the saved approval isn't the latest recorded one.
    /// </summary>
    /// <param name="saved">The latest saved approval's hash, or null.</param>
    /// <param name="recorded">The hash the latest recorded approval names, or null.</param>
    /// <param name="live">The live profile's hash, or null when it doesn't read.</param>
    /// <returns>True when approving is needed.</returns>
    public static bool NeedsApproval(ProfileHash? saved, ProfileHash? recorded, ProfileHash? live) =>
        (live ?? saved) is not { } judged || !IsApproved(saved, recorded, judged);

    private static SindriProfile Canonical(SindriProfile profile) => profile with
    {
        Repos = [.. (profile.Repos ?? []).OrderBy(r => r.Name, StringComparer.Ordinal)],
        Services = new SortedDictionary<string, ServiceSettings>(
            (profile.Services ?? SindriProfile.NoServices).ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal),
        Api = profile.Api ?? new ApiSettings(),
    };

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
