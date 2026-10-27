using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>
/// The rules a map build runs under, as one stable hash (issue 1041): the built-in <see cref="RulesVersion"/> and, per mapped repo, its deny globs, demote globs and concept pins.
/// A map version stores the hash it was built with; the refresh tick and the doctor compare it with the running one, so a map built under other rules is rebuilt and named.
/// </summary>
public static class MapRuleset
{
    /// <summary>The version of the built-in rules (the clustering and extraction code). Bump it in every change to those rules, so every map built before it is rebuilt once.</summary>
    public const string RulesVersion = "1";

    private const char Unit = '\u001f';

    /// <summary>The hash of the rules for <paramref name="repos"/>; the same for the same rules in any order.</summary>
    /// <param name="repos">The mapped repos with their deny globs and concept pins.</param>
    /// <returns>The SHA-256 of the canonical text, as lowercase hex.</returns>
    public static string Of(IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var lines = new List<string> { Line("rules", RulesVersion) };
        foreach (var repo in repos)
        {
            var name = repo.Name.Value;
            lines.AddRange(repo.DenyPaths.Select(glob => Line("deny", name, glob)));
            lines.AddRange(repo.DemotePaths.Select(glob => Line("demote", name, glob)));
            lines.AddRange(repo.Concepts.Select(pin => Line("concept", name, Pin(pin))));
            lines.Add(Line("repo", name));
        }

        lines.Sort(StringComparer.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    /// <summary>The hash of the rules <paramref name="scope"/> would build under now, from its settings and repos.</summary>
    /// <param name="scope">The scope, or null.</param>
    /// <returns>The hash, or null when there is no scope or its settings can't be read (nothing to compare then).</returns>
    public static string? Running(ContextScope? scope) =>
        scope is not null && SettingsReader.Read(scope.Settings) is { IsSuccess: true } settings ? Of(ScopeRules.Mapped(settings.Value, scope.Repos)) : null;

    private static string Pin(ConceptPin pin) =>
        $"{pin.Id?.Value}{Unit}{pin.Name}{Unit}{pin.Summary}{Unit}{Sorted(pin.Features)}{Unit}{Sorted(pin.Paths)}";

    private static string Sorted(IReadOnlyList<string> values) => string.Join(',', values.Order(StringComparer.Ordinal));

    private static string Line(string kind, string first, string? second = null) => second is null ? $"{kind}{Unit}{first}" : $"{kind}{Unit}{first}{Unit}{second}";
}
