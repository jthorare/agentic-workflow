using System.Diagnostics.CodeAnalysis;

namespace Aw.Judge.Core.Labels;

/// <summary>The label sources' wire names, as the 'labels' table, the CLI flag and the reports spell them.</summary>
public static class LabelSources
{
    /// <summary>The wire name, e.g. 'adjudicator'.</summary>
    /// <param name="value">The source.</param>
    /// <returns>The name.</returns>
    public static string Name(LabelSource value) => value switch
    {
        LabelSource.Outcome => "outcome",
        LabelSource.Adjudicator => "adjudicator",
        LabelSource.Override => "override",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown label source"),
    };

    /// <summary>Reads a wire name, exactly (no trimming, case-sensitive).</summary>
    /// <param name="text">The name.</param>
    /// <param name="value">The source, when the name is one.</param>
    /// <returns>Whether it is a label source name.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out LabelSource value)
    {
        (var ok, value) = text switch
        {
            "outcome" => (true, LabelSource.Outcome),
            "adjudicator" => (true, LabelSource.Adjudicator),
            "override" => (true, LabelSource.Override),
            _ => (false, default),
        };
        return ok;
    }
}
