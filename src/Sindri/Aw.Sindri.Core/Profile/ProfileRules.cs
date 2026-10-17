using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

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

        var invalid = Services(profile.Services) ?? Workflow(profile.Workflow) ?? Globs(profile);
        if (invalid is not null)
        {
            return invalid;
        }

        return Canonical(profile);
    }

    /// <summary>The longest glob 'index.denyPaths' takes.</summary>
    private const int MaxGlob = 300;

    /// <summary>Checks every 'index.denyPaths' glob (the profile's and each repo's): relative to the repo, '/'-separated, no '..'.</summary>
    private static Error? Globs(SindriProfile profile)
    {
        var lists = new List<(string Path, IReadOnlyList<string> Globs)>();
        if (profile.Index?.DenyPaths is { } shared)
        {
            lists.Add(("index.denyPaths", shared));
        }

        lists.AddRange((profile.Repos ?? []).Where(r => r.Index?.DenyPaths is not null).Select(r => ($"repos.{r.Name}.index.denyPaths", r.Index!.DenyPaths!)));   // why: the filter keeps repos with a list
        foreach (var (path, globs) in lists)
        {
            for (var i = 0; i < globs.Count; i++)
            {
                if (GlobProblem(globs[i]) is { } problem)
                {
                    return Invalid(string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), problem);
                }
            }
        }

        return null;
    }

    private static string? GlobProblem(string glob) => true switch
    {
        _ when string.IsNullOrWhiteSpace(glob) => "must be a non-empty glob",
        _ when glob.Length > MaxGlob => string.Create(CultureInfo.InvariantCulture, $"is over {MaxGlob} characters"),
        _ when glob.StartsWith('/') || glob.Contains(':', StringComparison.Ordinal) => "must be relative to the repo root (no leading '/' and no drive or scheme)",
        _ when glob.Contains('\\', StringComparison.Ordinal) => "must use '/' separators",
        _ when glob.Split('/').Contains("..") => "must not contain '..'",
        _ when glob.Contains('\0', StringComparison.Ordinal) => "must not contain a NUL character",
        _ => null,
    };

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

    /// <summary>Each service's name and cadence.</summary>
    private static Error? Services(IReadOnlyDictionary<string, ServiceSettings>? services)
    {
        foreach (var (name, settings) in services ?? SindriProfile.NoServices)
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

        return null;
    }

    /// <summary>The 'workflow:' durations (S1-D11): each key that is set must be a duration such as '1m', '4h' or '2d'.</summary>
    private static Error? Durations(WorkflowSettings? workflow)
    {
        foreach (var (key, value) in new[] { ("deskTimeout", workflow?.DeskTimeout), ("eventHold", workflow?.EventHold) })
        {
            if (value is not null && !RecipeDuration.TryFrom(value, out _))
            {
                return Invalid($"workflow.{key}", $"'{value}' is not a duration: a whole number and m, h or d (for example 1m, 4h, 2d)");
            }
        }

        return null;
    }

    /// <summary>
    /// The effect keys (S1-D11, engine P6): each 'workflow.effects' entry is an 'effect.*' block id, and
    /// 'workflow.dispatchDelay' reads like a service cadence (seconds allowed, 5s to 24h).
    /// </summary>
    private static Error? Effects(WorkflowSettings? workflow)
    {
        var effects = workflow?.Effects ?? [];
        for (var i = 0; i < effects.Count; i++)
        {
            if (!EffectSettings.IsEffectId(effects[i]))
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"workflow.effects[{i}]"),
                    $"'{effects[i]}' is not an effect id: 'effect.' and a block name (for example effect.notify.desk)");
            }
        }

        return workflow?.DispatchDelay is { } delay && EffectSettings.Delay(delay) is null
            ? Invalid("workflow.dispatchDelay", $"'{delay}' is not a delay: a whole number and s, m or h (for example 10s), from 5s to 24h")
            : null;
    }

    private static Error? Workflow(WorkflowSettings? workflow) =>
        Durations(workflow) ?? Effects(workflow) ?? ForgeRules.Check(workflow?.Forge);

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
