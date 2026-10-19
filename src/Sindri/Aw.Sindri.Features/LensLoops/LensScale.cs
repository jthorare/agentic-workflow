namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// A lens loop's severity scale (line spec §5.2): most severe first. The first <see cref="Stops"/> severities stop the loop, because both loops'
/// exits are fixed on them (0 CRITICAL and 0 HIGH for the design lens loop; 0 blocking and 0 issue for the review loop).
/// </summary>
/// <param name="Severities">The severities, most severe first, in lower case.</param>
internal sealed record LensScale(IReadOnlyList<string> Severities)
{
    /// <summary>How many of the most severe severities stop a round.</summary>
    internal const int Stops = 2;

    /// <summary>The design lens pack's scale: CRITICAL, HIGH, MEDIUM, LOW.</summary>
    internal static LensScale Design { get; } = new(["critical", "high", "medium", "low"]);

    /// <summary>The review lens pack's scale: blocking, issue, suggestion, nit.</summary>
    internal static LensScale Review { get; } = new(["blocking", "issue", "suggestion", "nit"]);

    /// <summary>The severity's place on the scale (0 is the most severe), ignoring case; -1 when it is not on the scale.</summary>
    /// <param name="severity">The severity text.</param>
    /// <returns>The rank.</returns>
    internal int Rank(string? severity)
    {
        for (var rank = 0; rank < Severities.Count; rank++)
        {
            if (string.Equals(Severities[rank], severity, StringComparison.OrdinalIgnoreCase))
            {
                return rank;
            }
        }

        return -1;
    }
}
