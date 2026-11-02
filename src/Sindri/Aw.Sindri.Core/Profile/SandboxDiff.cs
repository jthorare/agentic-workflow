using System.Globalization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox:' key paths a profile sets, for 'profile approve' to compare (sandbox spec §9). An absent key has no path, so adding one shows as a change.</summary>
internal static class SandboxDiff
{
    internal static IEnumerable<(string Key, string Value)> Keys(SandboxSettings? sandbox) =>
        (sandbox is null ? [] : Limits(sandbox.Limits).Concat(Fs(sandbox.Fs)).Concat(Network(sandbox.Network)).Concat(Singles(sandbox)).Concat(Repos(sandbox.Repos)))
            .Where(static k => k.Value is not null)
            .Select(static k => (k.Key, k.Value!));   // why: the nulls are filtered out just before

    private static IEnumerable<(string Key, string? Value)> Limits(SandboxLimits? limits) => Sizes(limits).Concat(Counts(limits));

    private static IEnumerable<(string Key, string? Value)> Sizes(SandboxLimits? limits) =>
    [
        ("sandbox.limits.memory", limits?.Memory),
        ("sandbox.limits.cpu", limits?.Cpu),
        ("sandbox.limits.total", limits?.Total),
        ("sandbox.limits.wallGrace", limits?.WallGrace),
        ("sandbox.limits.homeTmpfs", limits?.HomeTmpfs),
        ("sandbox.limits.parkMax", limits?.ParkMax),
    ];

    private static IEnumerable<(string Key, string? Value)> Counts(SandboxLimits? limits) =>
    [
        ("sandbox.limits.tasks", limits?.Tasks?.ToString(CultureInfo.InvariantCulture)),
        ("sandbox.limits.sessions", limits?.Sessions?.ToString(CultureInfo.InvariantCulture)),
    ];

    private static IEnumerable<(string Key, string? Value)> Fs(SandboxFs? fs) =>
    [
        ("sandbox.fs.homeAllow", Join(fs?.HomeAllow?.Select(Entry))),
        ("sandbox.fs.denyExtra", Join(fs?.DenyExtra)),
    ];

    private static IEnumerable<(string Key, string? Value)> Network(SandboxNetwork? network) =>
        new[] { ("sandbox.network.mode", network?.Mode is { } mode ? SandboxWire.Name(mode) : null) }
            .Concat((network?.Groups ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)).Select(g => ($"sandbox.network.groups.{g.Key}", Join(g.Value))));

    private static IEnumerable<(string Key, string? Value)> Singles(SandboxSettings sandbox) =>
    [
        ("sandbox.inner", sandbox.Inner is { } inner ? SandboxWire.Name(inner) : null),
        ("sandbox.credentials.github", sandbox.Credentials?.Github is { } github ? SandboxWire.Name(github) : null),
        ("sandbox.login.source", sandbox.Login?.Source is { } source ? SandboxWire.Name(source) : null),
        ("sandbox.attended.wall", sandbox.Attended?.Wall),
        .. (sandbox.Login?.Targets ?? new Dictionary<string, ModelLoginSource>(StringComparer.Ordinal)).Select(t => ($"sandbox.login.targets.{t.Key}", (string?)SandboxWire.Name(t.Value))),
    ];

    private static IEnumerable<(string Key, string? Value)> Repos(IReadOnlyDictionary<string, SandboxRepoSettings>? repos) =>
        (repos ?? new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal)).SelectMany(r => Repo(r.Key, r.Value));

    private static IEnumerable<(string Key, string? Value)> Repo(string repo, SandboxRepoSettings settings)
    {
        var at = $"sandbox.repos.{repo}";
        return
        [
            (at + ".homeAllow", Join(settings.HomeAllow?.Select(Entry))),
            (at + ".groups", Join(settings.Groups)),
            (at + ".hosts", Join(settings.Hosts)),
            (at + ".cachePaths", Join(settings.CachePaths)),
            (at + ".login.source", settings.Login?.Source is { } source ? SandboxWire.Name(source) : null),
            (at + ".prepare.dirs", Join(settings.Prepare?.Dirs?.Select(Dir))),
        ];
    }

    private static string? Join(IEnumerable<string>? values) => values is null ? null : string.Join(", ", values);

    private static string Entry(HomeAllowEntry entry) => $"{entry.Path}:{(entry.Mode is { } mode ? SandboxWire.Name(mode) : "ro")}";

    private static string Dir(PrepareDir dir) => $"{dir.Dir}:{dir.Tool ?? "npm ci"}:{string.Join('+', dir.AllowScripts ?? [])}";
}
