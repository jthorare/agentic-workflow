using System.Globalization;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Testing;

/// <summary>The builder root, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A ticket.json document.</summary>
    /// <param name="brief">The brief.</param>
    /// <param name="expected">The expected behaviour.</param>
    /// <param name="source">The ticket's source (linear, text or github).</param>
    /// <param name="id">The tracker's key.</param>
    /// <returns>The JSON text.</returns>
    public static string TicketJson(string brief = "Login fails\\n\\nClicking Log in shows a blank page.", string expected = "The dashboard opens", string source = "linear", string id = "ENG-1") =>
        $$"""{"source":"{{source}}","id":"{{id}}","title":"Login fails","brief":"{{brief}}","expected":"{{expected}}","actual":"A blank page"}""";

    /// <summary>A /rootCause handoff.</summary>
    /// <param name="status">The status line's value.</param>
    /// <param name="results">Each hypothesis's result column, numbered from 1.</param>
    /// <param name="rootCause">The Root Cause section's text; null leaves the section out.</param>
    /// <returns>The markdown.</returns>
    public static string Handoff(string status = "diagnosed", string[]? results = null, string? rootCause = "The session cookie is dropped on redirect.")
    {
        var rows = (results ?? ["confirmed", "untested"]).Select(static (r, i) => string.Create(CultureInfo.InvariantCulture, $"| {i + 1} | Cause {i + 1} | `src/app.ts`, `src/b.ts` | high | {r} |"));
        var cause = rootCause is null ? string.Empty : $"\n## Root Cause\n\n{rootCause}\n";
        return $"# Investigation\n\nstatus: {status}\n\n## Hypotheses\n\n| # | Hypothesis | Cause-site files | Likelihood | Result |\n|---|---|---|---|---|\n{string.Join('\n', rows)}\n{cause}\n## Next\n\nfix it\n";
    }

    /// <summary>A judge answer.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="aboutInput">The input hash judge names; empty means the input it was asked about.</param>
    /// <returns>The answer, with an id unless it is an escalation.</returns>
    public static JudgeAnswer Verdict(JudgeDecision decision, string aboutInput = "") =>
        new(decision, decision == JudgeDecision.Escalated ? null : JudgeDecisionId.From("d-42"), "judged", ["the reported page now opens"], aboutInput);
}
