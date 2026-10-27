using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The pure decision for an agent run whose session ended without a report (issue #1000): the agent may have written its outputs and failed to report them because the
/// control plane was down. Outputs are the proof of finished work; the outcome is never guessed. The run completes only when exactly one of the Step's outcomes has outputs the
/// caller writes and every one of them is in the output folder; any other state of the folder (nothing, some outputs, several outcomes' worth, a Step that picks its outcome from
/// its parameters) parks.
/// </summary>
internal static class AgentRecovery
{
    /// <summary>The folder in the run directory the agent writes its outputs to (the launcher makes it; the agent brief names the same folder).</summary>
    public const string OutputFolder = ".sindri-out";

    private const string FactPrefix = "fact:";

    /// <summary>Decides what to do with the run.</summary>
    /// <param name="descriptor">The Step's descriptor, or null when the catalog doesn't know it.</param>
    /// <param name="files">The names of the files in the output folder.</param>
    /// <param name="folder">The output folder's path, for the reason.</param>
    /// <returns>Complete with the one outcome the outputs prove, else a park whose reason names the folder.</returns>
    public static AgentRecoveryDecision Decide(BlockDescriptor? descriptor, IReadOnlyCollection<string> files, string folder)
    {
        ArgumentNullException.ThrowIfNull(files);
        var reason = $"the agent finished but its result was not recorded: {folder}";
        if (descriptor is null || descriptor.OutcomesFrom is not null)
        {
            return AgentRecoveryDecision.Park(reason + " (fix: read the outputs there and report with 'sindri workflow complete')");
        }

        var provable = new List<(OutcomeName Outcome, Dictionary<FactKey, string> Files)>();
        foreach (var outcome in descriptor.Outcomes)
        {
            var needed = AgentStepBrief.Needed(descriptor, outcome).Distinct().ToList();
            var found = new Dictionary<FactKey, string>();
            foreach (var fact in needed)
            {
                if (files.FirstOrDefault(n => string.Equals(n, NameOf(fact), StringComparison.Ordinal)) is { } name)
                {
                    found[fact] = name;
                }
            }

            if (needed.Count > 0 && found.Count == needed.Count)
            {
                provable.Add((outcome, found));
            }
        }

        return provable is [var only]
            ? AgentRecoveryDecision.Complete(only.Outcome, only.Files)
            : AgentRecoveryDecision.Park(reason + " (fix: read the outputs there and report with 'sindri workflow complete')");
    }

    /// <summary>The file an agent writes a fact to: the fact's name without its 'fact:' prefix.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The file name.</returns>
    public static string NameOf(FactKey fact)
    {
        var text = fact.Value;
        return text.StartsWith(FactPrefix, StringComparison.Ordinal) ? text[FactPrefix.Length..] : text;
    }
}
