namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The figures all runs add to: one row per state or gate, the human-time samples and the budget parks.</summary>
internal sealed class StatsTally
{
    private readonly Dictionary<string, StatsFigures> _rows = new(StringComparer.Ordinal);

    public IEnumerable<StatsFigures> Rows => _rows.Values;

    public List<double> ParkSeconds { get; } = [];

    public List<double> EscalationSeconds { get; } = [];

    public List<double> TakeoverSeconds { get; } = [];

    public int BudgetHits { get; set; }

    public StatsFigures Row(string name, bool isGate)
    {
        if (!_rows.TryGetValue(name, out var row))
        {
            _rows[name] = row = new StatsFigures(name, isGate);
        }

        return row;
    }
}
