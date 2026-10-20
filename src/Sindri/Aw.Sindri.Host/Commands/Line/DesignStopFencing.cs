using Aw.Context.Core.Trust;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Host.Commands.Line;

/// <summary>
/// Fences the stop view's text for a session caller (line spec LN-API2: "untrusted text fenced for sessions"). The spec, the questions, answers, items, signals, quotes and findings
/// come from models and people, so each is wrapped in the pack fence; the ids, enums, numbers and structure are ours and stay plain. A human gets the view as it is.
/// </summary>
internal static class DesignStopFencing
{
    private const string Source = "design";

    /// <summary>The view with every piece of model- or person-written text inside the pack fence.</summary>
    internal static DesignStopView Fence(DesignStopView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view with
        {
            Accepted = [.. view.Accepted.Select(static a => a with { Text = F(a.Text), Resolution = F(a.Resolution) })],
            Models = view.Models with { Warnings = [.. view.Models.Warnings.Select(F)] },
            Decisions = [.. view.Decisions.Select(static d => d with { Question = F(d.Question), Options = [.. d.Options.Select(static o => o with { Answer = F(o.Answer), Tradeoffs = F(o.Tradeoffs) })] })],
            Acceptance = new StopAcceptance([.. view.Acceptance.Added.Select(static i => i with { Text = F(i.Text) })], [.. view.Acceptance.Superseded.Select(static i => i with { Text = F(i.Text) })]),
            Escapes = [.. view.Escapes.Select(static e => e with { Why = F(e.Why) })],
            Graph = view.Graph with { Phases = [.. view.Graph.Phases.Select(static p => p with { Title = F(p.Title), TestChanges = [.. p.TestChanges.Select(static t => t with { Reason = F(t.Reason) })] })] },
            Trace = [.. view.Trace.Select(static r => r with { Signals = [.. r.Signals.Select(static s => s with { Statement = F(s.Statement) })] })],
            Coverage = [.. view.Coverage.Select(static c => c with { Excluded = c.Excluded is null ? null : F(c.Excluded) })],
            Spec = view.Spec with { Text = F(view.Spec.Text) },
            Contracts = [.. view.Contracts.Select(static c => c with { Quote = F(c.Quote), Change = F(c.Change) })],
            LensRounds = [.. view.LensRounds.Select(static r => r with { Lenses = [.. r.Lenses.Select(static l => l with { Findings = [.. l.Findings.Select(static f => f with { Text = F(f.Text), Resolution = f.Resolution is null ? null : F(f.Resolution) })] })] })],
            FreezeRefused = view.FreezeRefused is null ? null : view.FreezeRefused with { Message = F(view.FreezeRefused.Message) },
        };
    }

    private static string F(string text) => text.Length == 0 ? text : UntrustedFence.Wrap(Source, author: null, text);
}
