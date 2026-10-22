using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>The arms by name: parsing '--arms', their order, and which exist yet.</summary>
public static class Arms
{
    /// <summary>The arms this build runs, in report order.</summary>
    public static IReadOnlyList<Arm> Available { get; } = [Arm.Brief, Arm.Docs, Arm.Graph, Arm.GraphReviewed, Arm.All];

    /// <summary>The ranker comparison's arms (S-8), in report order: only '--arms ranker' (or each by name) runs them, never 'all', because the rerank row asks a model.</summary>
    public static IReadOnlyList<Arm> Ranker { get; } = [Arm.RankerRrf, Arm.RankerFeatures, Arm.RankerRerank];

    /// <summary>The arms that print "not available", in report order.</summary>
    public static IReadOnlyList<Arm> NotAvailable { get; } = [Arm.Linear, Arm.Onyx, Arm.Code];

    /// <summary>The arm's name as '--arms' and the report write it.</summary>
    /// <param name="arm">The arm.</param>
    /// <returns>'brief', 'docs', 'graph', 'graph+reviewed', 'linear', 'onyx', 'code' or 'all'.</returns>
    public static string Name(Arm arm) => arm switch
    {
        Arm.Brief => "brief",
        Arm.Docs => "docs",
        Arm.Graph => "graph",
        Arm.GraphReviewed => "graph+reviewed",
        Arm.Linear => "linear",
        Arm.Onyx => "onyx",
        Arm.Code => "code",
        Arm.All => "all",
        Arm.RankerRrf => "ranker-rrf",
        Arm.RankerFeatures => "ranker-features",
        Arm.RankerRerank => "ranker-rerank",
        _ => throw new ArgumentOutOfRangeException(nameof(arm)),
    };

    /// <summary>The stage that brings a not-yet-available arm.</summary>
    /// <param name="arm">The arm.</param>
    /// <returns>'S-6', 'S-7B' or 'S-9'; null for an arm that runs.</returns>
    public static string? PendingStage(Arm arm) => arm switch
    {
        Arm.Linear => "S-6",
        Arm.Onyx => "S-7B",
        Arm.Code => "S-9",
        Arm.Brief or Arm.Docs or Arm.Graph or Arm.GraphReviewed or Arm.All or Arm.RankerRrf or Arm.RankerFeatures or Arm.RankerRerank => null,
        _ => throw new ArgumentOutOfRangeException(nameof(arm)),
    };

    /// <summary>Parses a comma-separated '--arms' value.</summary>
    /// <param name="text">The value; 'all' (alone) expands to <see cref="Available"/>.</param>
    /// <returns>The arms in report order without repeats, or SND-CONTEXT-082 naming the arms.</returns>
    public static Result<IReadOnlyList<Arm>> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0)
        {
            return Unknown(text);
        }

        if (names.Length == 1 && string.Equals(names[0], "all", StringComparison.Ordinal))
        {
            return Available.ToList();
        }

        names = [.. names.SelectMany(static n => string.Equals(n, "ranker", StringComparison.Ordinal) ? Ranker.Select(Name) : [n])];

        var parsed = new List<Arm>();
        foreach (var name in names)
        {
            var arm = Available.Concat(Ranker).Concat(NotAvailable).Where(a => string.Equals(Name(a), name, StringComparison.Ordinal)).Cast<Arm?>().FirstOrDefault();
            if (arm is not { } found)
            {
                return Unknown(name);
            }

            parsed.Add(found);
        }

        return Available.Concat(Ranker).Concat(NotAvailable).Where(parsed.Contains).ToList();
    }

    private static Error Unknown(string name)
    {
        var listed = string.Join(", ", Available.Concat(Ranker).Select(Name)) + " ('ranker' runs the three ranker arms); not available yet: " + string.Join(", ", NotAvailable.Select(a => $"{Name(a)} ({PendingStage(a)})"));
        return ContextErrors.UnknownArm.With($"'{Printable(name)}' is not an arm (arms: {listed}); fix: use a listed arm or 'all'");
    }

    private static string Printable(string text) => new([.. text.Where(c => !char.IsControl(c)).Take(40)]);
}
