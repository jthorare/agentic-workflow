using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// Walks one run's rows in ledger order, adding to the shared tally and building the run: gates evaluated, parks, escalations and takeovers
/// as they begin and end, time in each state, tokens and the outcome.
/// </summary>
/// <param name="tally">The figures every run adds to.</param>
internal sealed class StatsRunWalker(StatsTally tally)
{
    private StatsRun _run = new(default);
    private string _state = string.Empty;
    private DateTimeOffset _entered;
    private StatsOpen? _open;

    public StatsRun Walk(IEnumerable<StatEvent> rows, StatEvent start)
    {
        _run = new StatsRun(start.At);
        _state = string.Empty;
        _entered = start.At;
        _open = null;
        foreach (var row in rows)
        {
            CountGates(row);
            if (row.HasDetail && _open?.Kind != row.Suspension)
            {
                Suspend(row);
            }

            Enter(row);
            _run.Tokens = row.TokensSpent is { } spent ? Math.Max(_run.Tokens ?? 0, spent) : _run.Tokens;
            if (row.Outcome is { } outcome)
            {
                (_run.Outcome, _run.EndedAt) = (outcome, row.At);
            }
        }

        return _run;
    }

    private void CountGates(StatEvent row)
    {
        foreach (var gate in row.Gates)
        {
            var figures = tally.Row(gate.Gate, isGate: true);
            figures.Evaluated++;
            figures.Refused += gate.Outcome is GateOutcome.Refused or GateOutcome.Unavailable ? 1 : 0;
        }
    }

    private void Suspend(StatEvent row)
    {
        if (_open is not null)
        {
            End(_open, row);
        }

        _open = row.Suspension is { } kind ? Begin(kind, row) : null;
    }

    private void Enter(StatEvent row)
    {
        if (row.To.Length == 0 || EngineStates.IsEngineOwned(row.To) || string.Equals(row.To, _state, StringComparison.Ordinal))
        {
            return;
        }

        if (_state.Length > 0)
        {
            tally.Row(_state, isGate: false).Seconds.Add((row.At - _entered).TotalSeconds);
        }

        (_state, _entered) = (row.To, row.At);
    }

    private StatsOpen Begin(SuspensionKind kind, StatEvent row)
    {
        switch (kind)
        {
            case SuspensionKind.Park:
                tally.Row(_state, isGate: false).Parks.Increment(StatsText.Park(row.ParkReason));
                tally.BudgetHits += row.ParkReason == ParkReason.Budget ? 1 : 0;
                break;
            case SuspensionKind.Escalation:
                CountEscalation(row);
                break;
            case SuspensionKind.Takeover:
                tally.Row(_state, isGate: false).Takeovers++;
                _run.Takeover = true;
                break;
            default:
                break;
        }

        return new StatsOpen(kind, row.At);
    }

    private void CountEscalation(StatEvent row)
    {
        var escalated = row.Gates.Where(static g => g.Outcome == GateOutcome.Escalated).ToList();
        if (escalated.Count == 0)
        {
            tally.Row(_state, isGate: false).Escalations.Increment(StatsText.Escalation(row.Escalation));
        }

        foreach (var gate in escalated)
        {
            var reason = gate.Threshold is { } t ? string.Create(CultureInfo.InvariantCulture, $"below {t:0.00}") : StatsText.Escalation(row.Escalation);
            tally.Row(gate.Gate, isGate: true).Escalations.Increment(reason);
        }
    }

    private void End(StatsOpen open, StatEvent row)
    {
        var seconds = (row.At - open.StartedAt).TotalSeconds;
        switch (open.Kind)
        {
            case SuspensionKind.Park when row.Answered:
                tally.ParkSeconds.Add(seconds);
                _run.ParkAnswered = true;
                break;
            case SuspensionKind.Escalation when row.Answered:
                tally.EscalationSeconds.Add(seconds);
                _run.EscalationDecided = true;
                break;
            case SuspensionKind.Takeover:
                tally.TakeoverSeconds.Add(seconds);
                break;
            case SuspensionKind.Park:
            case SuspensionKind.Escalation:
            default:
                break;
        }
    }
}
