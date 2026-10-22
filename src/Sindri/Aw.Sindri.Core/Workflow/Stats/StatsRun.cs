namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One run (an instance that started in the window) as the walk over its rows builds it up.</summary>
/// <param name="startedAt">When the run started.</param>
internal sealed class StatsRun(DateTimeOffset startedAt)
{
    public DateTimeOffset StartedAt { get; } = startedAt;

    public DateTimeOffset? EndedAt { get; set; }

    public string? Outcome { get; set; }

    public int? Tokens { get; set; }

    public bool ParkAnswered { get; set; }

    public bool EscalationDecided { get; set; }

    public bool Takeover { get; set; }

    public bool NeededHuman => ParkAnswered || EscalationDecided || Takeover;

    public StatsEnding Class => Outcome switch
    {
        null => StatsEnding.Open,
        "resolved" or "alreadySatisfied" => StatsEnding.Done,
        "cancelled" => StatsEnding.Cancelled,
        _ => StatsEnding.Failed,
    };
}
