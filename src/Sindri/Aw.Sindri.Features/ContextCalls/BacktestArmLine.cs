using System.Text.Json.Serialization;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One arm in a backtest job's result.</summary>
/// <param name="Arm">The arm's name.</param>
/// <param name="NotAvailable">The stage that brings an arm that doesn't run yet, else null.</param>
/// <param name="Overall">The arm's overall line ('PASS', 'NOT PASSED', 'not measured', the owner-decision line), or null when it did not run.</param>
/// <param name="Leaky">True for the arm that reads the reviewed map, which knew the answers (labeled leaky in every row).</param>
/// <param name="Score">The arm's numbers (the rows the CLI prints), or null when it did not run.</param>
public sealed record BacktestArmLine(string Arm, string? NotAvailable, string? Overall, bool Leaky, BacktestArmScore? Score)
{
    /// <summary>On a ranker arm: its recall@10 and MRR over only the pin-anchored questions (count 0 when none were); null on any other arm.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PinAnchoredScore? PinAnchored { get; init; }
}
