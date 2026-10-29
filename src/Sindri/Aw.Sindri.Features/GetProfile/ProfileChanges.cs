using System.Globalization;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>What the live profile asks for beyond the approved one, and the words for it (setup-once spec §5.2 "Preview"). Pure: file facts come from the caller.</summary>
/// <param name="NewRepos">The repos the live profile names and the approved one does not.</param>
/// <param name="Services">One sentence per service enabled or run more often.</param>
/// <param name="Allowance">One sentence per effect newly allowed, then one per budget or limit added or raised.</param>
/// <param name="Connections">One sentence per outside connection added or changed.</param>
/// <param name="HumanOnly">One sentence per step the profile keeps for the owner alone, newly.</param>
internal sealed record ProfileChanges(IReadOnlyList<RepoEntry> NewRepos, IReadOnlyList<string> Services, IReadOnlyList<string> Allowance, IReadOnlyList<string> Connections, IReadOnlyList<string> HumanOnly)
{
    /// <summary>Gets a value indicating whether the live profile asks for nothing new.</summary>
    public bool IsEmpty => NewRepos.Count == 0 && Services.Count == 0 && Allowance.Count == 0 && Connections.Count == 0 && HumanOnly.Count == 0;

    /// <summary>What <paramref name="live"/> asks for that <paramref name="approved"/> does not.</summary>
    /// <param name="approved">The approved profile, or null when nothing is approved yet.</param>
    /// <param name="live">The live profile.</param>
    /// <param name="sentences">The plain sentence of each effect block.</param>
    /// <returns>The changes.</returns>
    public static ProfileChanges Between(SindriProfile? approved, SindriProfile live, IBlockSentences sentences)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(sentences);
        return new ProfileChanges(
            AbsentRepos(approved, live),
            [.. ServiceSentences(approved?.Services ?? SindriProfile.NoServices, live.Services ?? SindriProfile.NoServices)],
            [.. NewEffects(approved, live, sentences).Concat(ProfileGroups.Allowance(approved, live))],
            [.. ProfileGroups.Connections(approved, live)],
            [.. ProfileGroups.HumanOnly(approved, live)]);
    }

    /// <summary>The three sentences of a repo the approval would add, in reading order.</summary>
    /// <param name="name">The repo's short name.</param>
    /// <param name="path">The real path of its checkout.</param>
    /// <returns>The sentences.</returns>
    public static IReadOnlyList<string> RepoSentences(string name, string path) =>
    [
        $"Sindri will read {path} and send what it reads to the map cleanup agent",
        $"Sindri will write its MCP settings into {name} for Claude Code, Codex and Cursor",
        $"Sindri will keep {name}'s map up to date on its own",
    ];

    private static List<RepoEntry> AbsentRepos(SindriProfile? approved, SindriProfile live)
    {
        var named = (approved?.Repos ?? []).Select(static r => r.Name).ToHashSet(StringComparer.Ordinal);
        return [.. (live.Repos ?? []).Where(r => !named.Contains(r.Name))];
    }

    private static IEnumerable<string> NewEffects(SindriProfile? approved, SindriProfile live, IBlockSentences sentences)
    {
        var allowed = (approved?.Workflow?.Effects ?? []).ToHashSet(StringComparer.Ordinal);
        return (live.Workflow?.Effects ?? []).Where(e => !allowed.Contains(e)).Distinct(StringComparer.Ordinal).Select(e => EffectSentence(e, sentences));
    }

    private static IEnumerable<string> ServiceSentences(IReadOnlyDictionary<string, ServiceSettings> approved, IReadOnlyDictionary<string, ServiceSettings> live)
    {
        foreach (var (name, settings) in live.OrderBy(static s => s.Key, StringComparer.Ordinal))
        {
            if (!approved.TryGetValue(name, out var before))
            {
                yield return $"Sindri will run the {name} service every {Words(settings.Every)}";
            }
            else if (Seconds(settings.Every) is { } now && Seconds(before.Every) is { } was && now < was)
            {
                yield return $"Sindri will run the {name} service more often: every {Words(settings.Every)} instead of every {Words(before.Every)}";
            }
        }
    }

    private static string EffectSentence(string effect, IBlockSentences sentences) =>
        BlockId.TryFrom(effect, out var id) && sentences.For(id) is { } sentence
            ? sentence
            : $"Allows the effect \"{Humanise(effect)}\"";

    private static string Humanise(string effect) =>
        (effect.StartsWith("effect.", StringComparison.Ordinal) ? effect["effect.".Length..] : effect).Replace('.', ' ').Replace('-', ' ').Replace('_', ' ');

    // A cadence is a whole number and s, m or h ('5s', '1h'); anything else is left as written.
    private static (long Count, char Unit)? Split(string every) =>
        every.Length > 1 && long.TryParse(every[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && every[^1] is 's' or 'm' or 'h' ? (count, every[^1]) : null;

    private static long? Seconds(string every) =>
        Split(every) is { } split ? split.Count * (split.Unit switch { 'h' => 3600L, 'm' => 60L, _ => 1L }) : null;

    private static string Words(string every) =>
        Split(every) is { } split
            ? string.Create(CultureInfo.InvariantCulture, $"{split.Count} {split.Unit switch { 'h' => "hour", 'm' => "minute", _ => "second" }}{(split.Count == 1 ? string.Empty : "s")}")
            : every;
}
