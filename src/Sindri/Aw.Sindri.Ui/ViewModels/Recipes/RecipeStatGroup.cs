using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>A titled group of figures on the recipe sheet's "How it has run": where a person came in, the steps, the weeks.</summary>
/// <param name="Heading">The group's heading.</param>
/// <param name="Lines">The figures, in order; never empty.</param>
internal sealed record RecipeStatGroup(string Heading, IReadOnlyList<RecipeStatLine> Lines)
{
    /// <summary>The groups a stats answer has something to say for; a group with no lines is left out.</summary>
    /// <param name="stats">The answer.</param>
    /// <returns>The groups, in order.</returns>
    internal static IReadOnlyList<RecipeStatGroup> Of(RecipeStatsView stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var groups = new List<RecipeStatGroup>();
        Add(groups, Labels.StatsHumanHeading, HumanLines(stats.HumanTime));
        Add(groups, Labels.StatsStepsHeading, [.. (stats.ByState ?? []).Select(StepLine)]);
        Add(groups, Labels.StatsWeeksHeading, [.. (stats.Buckets ?? []).Where(b => b.Runs > 0).Select(b => new RecipeStatLine(Labels.StatWeek(b.WeekStart), Labels.StatWeekLine(b.Runs, b.HumanRate)))]);
        return groups;
    }

    private static void Add(List<RecipeStatGroup> groups, string heading, List<RecipeStatLine> lines)
    {
        if (lines.Count > 0)
        {
            groups.Add(new RecipeStatGroup(heading, lines));
        }
    }

    private static List<RecipeStatLine> HumanLines(StatsHumanTimeView? human)
    {
        var lines = new List<RecipeStatLine>();
        if (human is null)
        {
            return lines;
        }

        AddHuman(lines, Labels.StatParksAnswered, human.ParksAnswered);
        AddHuman(lines, Labels.StatEscalationsDecided, human.EscalationsDecided);
        AddHuman(lines, Labels.StatTakeovers, human.Takeovers);
        return lines;
    }

    private static void AddHuman(List<RecipeStatLine> lines, string label, HumanTimeFigureView figure)
    {
        if (figure.N > 0)
        {
            lines.Add(new RecipeStatLine(label, Labels.StatCountAndWait(figure.N, figure.MedianSeconds)));
        }
    }

    // One step in words: the typical time, then only what happened there (parks and escalations by reason, takeovers, the gate's refusals).
    private static RecipeStatLine StepLine(StateStatsView step)
    {
        var parts = new List<string>();
        if (step.MedianSeconds is { } seconds)
        {
            parts.Add(Labels.Duration(seconds));
        }

        AddReasons(parts, "parked", step.Parks);
        AddReasons(parts, "escalated", step.Escalations);
        if (step.Takeovers > 0)
        {
            parts.Add("taken over " + Labels.StatCount(step.Takeovers));
        }

        if (step.GateRefusals is { Refused: > 0 } gate)
        {
            parts.Add($"gate refused {Labels.StatCount(gate.Refused)} of {Labels.StatCount(gate.Evaluated)}");
        }

        return new RecipeStatLine(step.State, Labels.StatStepLine(parts));
    }

    private static void AddReasons(List<string> parts, string verb, IReadOnlyList<ReasonCountView> reasons)
    {
        if (reasons.Count > 0)
        {
            parts.Add($"{verb} {Labels.StatCount(reasons.Sum(r => r.N))} ({string.Join(", ", reasons.Select(r => Labels.StatReason(r.Reason)))})");
        }
    }
}
