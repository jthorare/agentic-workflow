using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Builds a recipe's <see cref="RecipeStats"/> from the runs the walk collected and the tally they added to.</summary>
internal static class StatsBuilder
{
    private const int Weeks = 4;

    public static RecipeStats Build(StatsRequest request, List<StatsRun> runs, StatsTally tally)
    {
        var done = runs.Where(static r => r.Class == StatsEnding.Done).ToList();
        var tokens = runs.Where(static r => r.Tokens is > 0).Select(static r => (double)r.Tokens!.Value).Order().ToList();   // why: the filter keeps only runs with a spend
        return new RecipeStats(
            request.Recipe.Value,
            request.Hash,
            request.Window.Text,
            ChildOnly: false,
            runs.Count,
            done.Count,
            runs.Count(static r => r.Class == StatsEnding.Failed),
            runs.Count(static r => r.Class == StatsEnding.Cancelled),
            new StatsOutcomes(done.Count(static r => !r.NeededHuman), done.Count(static r => !r.Takeover && r.NeededHuman), done.Count(static r => r.Takeover)),
            HumanRate(runs),
            StatsMath.Median(runs.Where(static r => r.EndedAt is not null).Select(static r => (r.EndedAt!.Value - r.StartedAt).TotalSeconds)),   // why: the filter keeps only ended runs
            new StatsTokens(StatsMath.Rank(tokens, 0.5), StatsMath.Rank(tokens, 0.9)),
            tally.BudgetHits,
            new StatsHumanTime(Figure(tally.ParkSeconds), Figure(tally.EscalationSeconds), Figure(tally.TakeoverSeconds)),
            [.. tally.Rows.Select(static r => r.Output())],
            Buckets(runs, request.Now),
            request.Now);
    }

    private static double? HumanRate(List<StatsRun> runs) => runs.Count == 0 ? null : (double)runs.Count(static r => r.NeededHuman) / runs.Count;

    private static List<StatsBucket> Buckets(List<StatsRun> runs, DateTimeOffset now)
    {
        var today = now.UtcDateTime.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var buckets = new List<StatsBucket>(Weeks);
        for (var week = Weeks - 1; week >= 0; week--)
        {
            var from = monday.AddDays(-7 * week);
            var inWeek = runs.Where(r => r.StartedAt.UtcDateTime >= from && r.StartedAt.UtcDateTime < from.AddDays(7)).ToList();
            buckets.Add(new StatsBucket(from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), inWeek.Count, HumanRate(inWeek)));
        }

        return buckets;
    }

    private static HumanTimeFigure Figure(List<double> seconds) => new(seconds.Count, StatsMath.Median(seconds));
}
