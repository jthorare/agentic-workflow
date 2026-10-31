using System.Globalization;
using System.Text;
using Aw.Context.Core.Eval.Backtest;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of a finished backtest job: the sample count line, one row per arm in report order, the pass line (verdicts are words, never colors).</summary>
internal static class BacktestJobText
{
    private const int NameWidth = 22;

    internal static string Render(RunContextBacktestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder(Invariant($"{result.Samples} samples · {result.LeftOut} left out{(result.SmallSample ? " · small sample" : string.Empty)}\n"));
        foreach (var arm in result.Arms)
        {
            _ = text.Append("  ").Append((arm.Arm + (arm.Leaky ? " (leaky)" : string.Empty)).PadRight(NameWidth)).Append(' ').AppendLine(Row(arm));
            if (arm.PinAnchored is { } pin)
            {
                _ = text.Append("    ").AppendLine(PinRow(pin));
            }
        }

        if (result.Arms.Any(a => a.PinAnchored is { Count: < BacktestScoring.MinPinAnchored }))
        {
            _ = text.AppendLine(Invariant($"fewer than {BacktestScoring.MinPinAnchored} pin-anchored questions: the hand check in tests/fixtures/backtest/pin-questions.md decides"));
        }

        _ = text.AppendLine(Invariant($"pass line: {result.PassLine ?? "not measured"}"));
        foreach (var note in result.Degraded)
        {
            _ = text.Append("degraded: ").AppendLine(note);
        }

        return text.Append("report ").AppendLine(result.Report).ToString();
    }

    private static string Row(BacktestArmLine arm) => arm switch
    {
        { NotAvailable: { } stage } => Invariant($"not available ({stage})"),
        { Score: { } s } => Invariant($"node recall {Show(s.NodeRecall)} · node precision {Show(s.NodePrecision)} · file recall@20 {Show(s.FileRecallAt20)} · leaky {s.LeakyShare:0.00} · tokens {s.Tokens}"),
        _ => "did not run",
    };

    private static string PinRow(PinAnchoredScore pin) => pin.Count == 0
        ? "pin-anchored (0 questions): recall@10 not measured, MRR not measured"
        : Invariant($"pin-anchored ({pin.Count} question{(pin.Count == 1 ? string.Empty : "s")}): recall@10 {pin.RecallAt10:0.00}, MRR {pin.Mrr:0.00}");

    private static string Show(double? value) => value is { } v ? v.ToString("0.00", CultureInfo.InvariantCulture) : "not measured";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
