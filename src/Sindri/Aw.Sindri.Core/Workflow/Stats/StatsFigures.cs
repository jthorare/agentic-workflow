namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One state's or one gate's counts while the rows are walked.</summary>
/// <param name="name">The state id, or the gate's block id.</param>
/// <param name="isGate">Whether it is a gate.</param>
internal sealed class StatsFigures(string name, bool isGate)
{
    public List<double> Seconds { get; } = [];

    public Dictionary<string, int> Parks { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Escalations { get; } = new(StringComparer.Ordinal);

    public int Takeovers { get; set; }

    public int Refused { get; set; }

    public int Evaluated { get; set; }

    public StateStats Output() =>
        new(name, isGate ? null : StatsMath.Median(Seconds), Reasons(Parks), Reasons(Escalations), Takeovers, isGate ? new GateRefusals(Refused, Evaluated) : null);

    private static List<ReasonCount> Reasons(Dictionary<string, int> counts) =>
        [.. counts.OrderByDescending(static c => c.Value).ThenBy(static c => c.Key, StringComparer.Ordinal).Select(static c => new ReasonCount(c.Key, c.Value))];
}
