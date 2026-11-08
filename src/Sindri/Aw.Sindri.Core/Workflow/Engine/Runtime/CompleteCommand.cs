using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The 'workflow complete' line a desk task shows (API spec §3.8 'desk runtime'): one formatter for the status and 'next' lines, the
/// allowed command and the refusal that points a session at the human, so they all list the same outputs.
/// </summary>
public static class CompleteCommand
{
    /// <summary>
    /// The command reporting the Step's outcome, with an '--output &lt;fact&gt;=&lt;file&gt;' flag for each output the caller has to write. One outcome is filled in
    /// (with its outputs); several are all named ('&lt;a|b|c&gt;') for the caller to pick from, with the outputs every one of them needs and, per outcome, those only it needs; none shows '&lt;outcome&gt;'.
    /// </summary>
    /// <param name="address">The instance address.</param>
    /// <param name="run">The open run.</param>
    /// <param name="outcomes">The outcomes the Step can report, as the task lists them.</param>
    /// <param name="outputs">The Step's declared outputs.</param>
    /// <returns>The line; engine-filled outputs are left out, the engine writes them.</returns>
    public static string Line(string address, RunToken run, IReadOnlyList<OutcomeName> outcomes, IEnumerable<OutputSpec> outputs)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(outputs);
        var line = new StringBuilder($"sindri workflow complete {address} --run {run.Value} --outcome ");
        if (outcomes.Count == 0)
        {
            return line.Append("<outcome>").ToString();
        }

        _ = line.Append(outcomes.Count == 1 ? outcomes[0].Value : "<" + string.Join('|', outcomes.Select(static o => o.Value)) + ">");
        var all = outputs as IReadOnlyList<OutputSpec> ?? [.. outputs];
        var common = all.Where(o => outcomes.All(outcome => StepOutputs.CallerWritesOn(o, outcome))).ToList();   // several outcomes: only what every one of them needs
        foreach (var output in common)
        {
            _ = line.Append(" --output ").Append(output.Fact.Value).Append("=<file>");
        }

        if (outcomes.Count > 1)
        {
            AppendExtras(line, outcomes, all, common);
        }

        return line.ToString();
    }

    /// <summary>Names, for each outcome, the outputs it needs beyond the ones every outcome needs, so a caller who picks it isn't refused for a missing fact.</summary>
    private static void AppendExtras(StringBuilder line, IReadOnlyList<OutcomeName> outcomes, IReadOnlyList<OutputSpec> all, List<OutputSpec> common)
    {
        foreach (var outcome in outcomes)
        {
            var extra = all.Where(o => !common.Contains(o) && StepOutputs.CallerWritesOn(o, outcome)).ToList();
            if (extra.Count > 0)
            {
                _ = line.Append(" (").Append(outcome.Value).Append(" also needs:").AppendJoin(string.Empty, extra.Select(static o => $" --output {o.Fact.Value}=<file>")).Append(')');
            }
        }
    }
}
