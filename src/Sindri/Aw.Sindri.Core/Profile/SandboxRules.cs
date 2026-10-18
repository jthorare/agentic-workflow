using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks of the profile's 'sandbox:' section that the JSON shape can't express (sandbox spec §9): sizes, percentages, durations, hosts and paths.</summary>
internal static partial class SandboxRules
{
    private const string DurationHint = "a duration: a whole number and m, h or d (for example 10m)";

    /// <summary>The first problem in <paramref name="sandbox"/>, or null.</summary>
    internal static Error? Check(SandboxSettings? sandbox) =>
        sandbox is null ? null : Limits(sandbox.Limits) ?? Fs(sandbox.Fs) ?? Network(sandbox.Network) ?? Repos(sandbox.Repos) ?? Attended(sandbox.Attended);

    private static Error? Limits(SandboxLimits? limits)
    {
        if (limits is null)
        {
            return null;
        }

        (string Key, bool Ok, string Hint)[] checks =
        [
            ("memory", Optional(limits.Memory, m => string.Equals(m, "auto", StringComparison.Ordinal) || Size.IsMatch(m)), "'auto' or a size such as 12GiB"),
            ("cpu", Optional(limits.Cpu, Percent), "a percent from 1% to 100%, such as 50%"),
            ("total", Optional(limits.Total, t => Percent(t) || Size.IsMatch(t)), "a percent from 1% to 100% or a size such as 40GiB"),
            ("homeTmpfs", Optional(limits.HomeTmpfs, Size.IsMatch), "a size such as 256MiB"),
            ("wallGrace", Optional(limits.WallGrace, IsDuration), DurationHint),
            ("parkMax", Optional(limits.ParkMax, IsDuration), DurationHint),
            ("tasks", (limits.Tasks ?? 1) is >= 1 and <= 1_000_000, "from 1 to 1000000"),
            ("sessions", (limits.Sessions ?? 1) is >= 1 and <= 64, "from 1 to 64"),
        ];
        var (key, _, hint) = checks.FirstOrDefault(c => !c.Ok);
        return key is null ? null : Problem($"sandbox.limits.{key}", hint);
    }

    private static Error? Fs(SandboxFs? fs) =>
        PathProblems((fs?.HomeAllow ?? []).Select(h => h.Path), "sandbox.fs.homeAllow", RelativePathProblem)
        ?? PathProblems(fs?.DenyExtra, "sandbox.fs.denyExtra", AbsolutePathProblem);

    private static Error? Network(SandboxNetwork? network) =>
        (network?.Groups ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal))
            .Select(g => string.IsNullOrWhiteSpace(g.Key) ? Problem("sandbox.network.groups", "a group name must be non-empty text") : HostsProblem(g.Value, $"sandbox.network.groups.{g.Key}"))
            .FirstOrDefault(e => e is not null);

    private static Error? Repos(IReadOnlyDictionary<string, SandboxRepoSettings>? repos) =>
        (repos ?? new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal)).Select(r => Repo(r.Key, r.Value)).FirstOrDefault(e => e is not null);

    private static Error? Repo(string name, SandboxRepoSettings repo)
    {
        var at = $"sandbox.repos.{name}";
        if (string.IsNullOrWhiteSpace(name))
        {
            return Problem("sandbox.repos", "a repo name must be non-empty text");
        }

        return (repo.Groups ?? []).Any(string.IsNullOrWhiteSpace)
            ? Problem(at + ".groups", "a group name must be non-empty text")
            : HostsProblem(repo.Hosts ?? [], at + ".hosts")
                ?? PathProblems((repo.HomeAllow ?? []).Select(h => h.Path), at + ".homeAllow", RelativePathProblem)
                ?? PathProblems(repo.CachePaths, at + ".cachePaths", RelativePathProblem)
                ?? Prepare(repo.Prepare, at + ".prepare");
    }

    private static Error? Prepare(SandboxPrepare? prepare, string at) =>
        (prepare?.Dirs ?? []).Select((dir, i) => PrepareDirProblem(dir, string.Create(CultureInfo.InvariantCulture, $"{at}.dirs[{i}]"))).FirstOrDefault(e => e is not null);

    private static Error? PrepareDirProblem(PrepareDir dir, string item)
    {
        if (RelativePathProblem(dir.Dir) is { } problem)
        {
            return Problem(item + ".dir", problem);
        }

        if (dir.Tool is not null && !string.Equals(dir.Tool, "npm ci", StringComparison.Ordinal))
        {
            return Problem(item + ".tool", "must be 'npm ci' (the only prepare tool)");
        }

        return (dir.AllowScripts ?? []).Any(string.IsNullOrWhiteSpace) ? Problem(item + ".allowScripts", "a package name must be non-empty text") : null;
    }

    private static Error? Attended(SandboxAttended? attended) =>
        attended?.Wall is { } wall && !string.Equals(wall, SandboxDefaults.AttendedWall, StringComparison.Ordinal) && !IsDuration(wall)
            ? Problem("sandbox.attended.wall", $"'{wall}' is not 'off' or a duration: a whole number and m, h or d (for example 8h)")
            : null;

    private static Error? HostsProblem(IReadOnlyList<string> hosts, string at) =>
        hosts.Select((host, i) => Host.IsMatch(host) ? null : Problem(string.Create(CultureInfo.InvariantCulture, $"{at}[{i}]"), "must be a host name such as registry.npmjs.org or *.nuget.org (no scheme, port or path)"))
            .FirstOrDefault(e => e is not null);

    private static Error? PathProblems(IEnumerable<string?>? paths, string at, Func<string?, string?> problemOf) =>
        (paths ?? []).Select((path, i) => problemOf(path) is { } problem ? Problem(string.Create(CultureInfo.InvariantCulture, $"{at}[{i}]"), problem) : null)
            .FirstOrDefault(e => e is not null);

    private static string? RelativePathProblem(string? path) => path switch
    {
        null or "" => "must be a non-empty path",
        _ when string.IsNullOrWhiteSpace(path) => "must be a non-empty path",
        _ when path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal) => "must be relative (no leading '/' and no drive or scheme)",
        _ when path.Split('/').Contains("..") => "must not contain '..'",
        _ => null,
    };

    private static string? AbsolutePathProblem(string? path) =>
        string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Split('/').Contains("..") ? "must be an absolute path with no '..'" : null;

    private static bool Optional(string? value, Func<string, bool> valid) => value is null || valid(value);

    private static bool IsDuration(string value) => RecipeDuration.TryFrom(value, out _);

    private static bool Percent(string value) =>
        value.EndsWith('%') && int.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 100;

    private static Error Problem(string key, string detail) => SindriErrors.ProfileInvalid.With($"{key}: {detail}");

    [GeneratedRegex("^[1-9][0-9]*(KiB|MiB|GiB|TiB)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Size { get; }

    [GeneratedRegex("^(\\*\\.)?[a-z0-9]+([.-][a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Host { get; }
}
