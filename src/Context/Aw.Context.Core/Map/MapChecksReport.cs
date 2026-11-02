using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>The structure measures of one map: numbers for every build, and what they decide (map structure spec §4).</summary>
/// <param name="Isolated">Leaf home features with no link to another feature or a table.</param>
/// <param name="OverCap">Leaf home features holding more than <see cref="MapChecks.SizeCap"/> files.</param>
/// <param name="NoFileEdges">Whether the map has at least 20 features to measure but no file-level depends-on, reads or writes edge at all (imports and data stores not extracted).</param>
/// <param name="Offenders">The five largest features of each measure that trips.</param>
public sealed record MapChecksReport(CheckMeasure Isolated, CheckMeasure OverCap, bool NoFileEdges, IReadOnlyList<CheckOffender> Offenders)
{
    /// <summary>The worst verdict of the two measures.</summary>
    public CheckVerdict Verdict => (CheckVerdict)Math.Max((int)Isolated.Verdict, (int)OverCap.Verdict);

    /// <summary>What these measures decide under <paramref name="mode"/>.</summary>
    /// <param name="mode">Whether a refusal stops the build.</param>
    /// <returns>The verdict, the error when refused, the build report's notes and the reason stored with the draft.</returns>
    public MapChecksOutcome Decide(ChecksMode mode)
    {
        var notes = new List<string>();
        var stored = new List<string>();
        Error? refusal = null;
        if (NoFileEdges)
        {
            notes.Add("structure: no file edges (imports and data stores not extracted)");
            stored.Add("structure no file edges");
        }

        foreach (var (measure, words) in new[] { (Isolated, _isolatedWords), (OverCap, _overCapWords) })
        {
            if (measure.Verdict == CheckVerdict.Pass)
            {
                continue;
            }

            var refuse = measure.Verdict == CheckVerdict.Refuse;
            var limit = Percent(refuse ? measure.RefusePermille : measure.DegradedPermille);
            if (refuse && mode == ChecksMode.Refuse)
            {
                refusal ??= words.Refusal(Percent(measure.Permille), limit);
                continue;
            }

            notes.Add($"structure: {words.Note(Percent(measure.Permille), limit)}");
            stored.Add(words.Stored(Percent(measure.Permille), limit));
        }

        var verdict = refusal is not null ? CheckVerdict.Refuse : notes.Count > 0 ? CheckVerdict.Degraded : CheckVerdict.Pass;
        return new MapChecksOutcome(verdict, refusal, notes, stored.Count == 0 ? null : string.Join("; ", stored));
    }

    private static readonly Words _isolatedWords = new(
        static (p, limit) => ContextErrors.MapUnconnected.With(
            $"{p} of features (a feature is a group of files) have no link to another feature or a table (limit {limit}). Likely causes, in order: a path alias the resolver can't follow, a data-store library it doesn't know, an extraction that failed part-way. Fix the cause, then build again; `sindri context map checks --build` lists the offenders."),
        static (p, limit) => $"{p} of features (a feature is a group of files) have no link to another feature or a table; limit {limit}",
        static (p, limit) => $"structure {p} isolated, limit {limit}");

    private static readonly Words _overCapWords = new(
        static (p, limit) => ContextErrors.MapOversized.With(
            $"{p} of features hold more than {MapChecks.SizeCap.ToString(CultureInfo.InvariantCulture)} files (limit {limit}). A folder with no subfolders can't be split: exclude generated or vendor folders in the profile's deny globs (`sindri context map checks --build` names the folders), then build again."),
        static (p, limit) => $"{p} of features hold more than {MapChecks.SizeCap.ToString(CultureInfo.InvariantCulture)} files; limit {limit}",
        static (p, limit) => $"size {p} over {MapChecks.SizeCap.ToString(CultureInfo.InvariantCulture)} files, limit {limit}");

    /// <summary>One line with both measures and their degraded limits, for a build's job text and 'context map checks': numbers only, never a feature key.</summary>
    public string Summary => $"structure: {Part(Isolated, "isolated")}, {Part(OverCap, string.Create(CultureInfo.InvariantCulture, $"over {MapChecks.SizeCap} files"))}";

    private static string Part(CheckMeasure measure, string label) => measure.Skipped is { } why
        ? $"{label} not measured ({why})"
        : $"{Percent(measure.Permille)} {label} (limit {Percent(measure.DegradedPermille)})";

    /// <summary>A share in tenths of a percent as words: 410 is "41%", 301 is "30.1%".</summary>
    /// <param name="permille">Tenths of a percent.</param>
    /// <returns>The words.</returns>
    public static string Percent(int permille) => permille % 10 == 0
        ? string.Create(CultureInfo.InvariantCulture, $"{permille / 10}%")
        : string.Create(CultureInfo.InvariantCulture, $"{permille / 10}.{permille % 10}%");

    private sealed record Words(Func<string, string, Error> Refusal, Func<string, string, string> Note, Func<string, string, string> Stored);
}
