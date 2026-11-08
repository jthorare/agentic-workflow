using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The short plain-text brief a launched agent session gets with its seed (engine P8 F13): where it is, what it reads, and the exact 'sindri workflow complete' command that ends its Step.</summary>
public static class AgentStepBrief
{
    private const string RunFolder = "$AW_SINDRI_RUN_DIR";
    private const string OutFolder = RunFolder + "/.sindri-out";   // why: the launcher makes and git-excludes this folder (StepFactWriter.Outputs), so outputs never count as worktree changes
    private const string FactPrefix = "fact:";

    /// <summary>Renders the brief; deterministic for the same run and descriptor.</summary>
    /// <param name="run">The run: the instance, the run token and the Step.</param>
    /// <param name="descriptor">The Step's descriptor, or null when the catalog doesn't know it.</param>
    /// <param name="instructions">What the Step is told to do (<see cref="IAgentStepInstructions.Lines"/>), after the first line; none for a Step that has no instructions.</param>
    /// <param name="factFiles">The names of the fact files the launch wrote into the run folder (generated for every Step, never per recipe); none when it wrote none.</param>
    /// <returns>The brief.</returns>
    public static string Render(AgentStepRun run, BlockDescriptor? descriptor, IReadOnlyList<string>? instructions = null, IEnumerable<string>? factFiles = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        var lines = new List<string> { $"Sindri Step brief. Instance {run.Address.Value}, run {run.Run.Value}, state {run.Step.State}." };
        lines.AddRange(instructions ?? []);
        if (descriptor is { Inputs.Count: > 0 })
        {
            lines.Add("You read: " + string.Join(", ", descriptor.Inputs.Select(static i => i.Value)) + ".");
        }

        if (factFiles?.Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } written)
        {
            lines.Add($"The facts you read that are set are already in the run folder {RunFolder}, one JSON file each: {string.Join(", ", written)}.");
        }

        var outcomes = descriptor?.Outcomes ?? [];
        lines.AddRange(outcomes.Select(o => $"Outcome {o.Value}: " + Writes(descriptor!, o) + "."));   // why: outcomes is empty without a descriptor
        if (descriptor?.OutcomesFrom is not null)
        {
            lines.Add("This Step also reports the choices its state lists; read them from the state's parameters.");
        }

        lines.Add($"Put each output in its own file under the output folder {OutFolder}, named after the fact.");
        lines.Add("Report with: " + Command(run, descriptor, outcomes));
        lines.Add("Your Step ends only when this command succeeds; if it's refused, read the error and fix it.");
        return string.Join('\n', lines);
    }

    private static string Command(AgentStepRun run, BlockDescriptor? descriptor, IReadOnlyList<OutcomeName> outcomes)
    {
        var head = $"sindri workflow complete {run.Address.Value} --run {run.Run.Value} --outcome ";
        if (descriptor is not null && descriptor.OutcomesFrom is null && outcomes.Count == 1)
        {
            var outputs = Needed(descriptor, outcomes[0]).Select(static f => $" --output {f.Value}={OutFolder}/{Name(f)}");
            return head + outcomes[0].Value + string.Concat(outputs);
        }

        var choice = outcomes.Count == 0 ? "<outcome>" : "<" + string.Join('|', outcomes.Select(static o => o.Value)) + ">";
        return head + choice + $" --output fact:<key>={OutFolder}/<key> (one --output per fact the outcome needs)";
    }

    private static string Writes(BlockDescriptor descriptor, OutcomeName outcome)
    {
        var needed = Needed(descriptor, outcome);
        return needed.Count == 0 ? "no outputs needed" : "write " + string.Join(", ", needed.Select(static f => f.Value));
    }

    /// <summary>The facts a caller writes when the Step reports <paramref name="outcome"/>: the outputs required on it that the engine doesn't fill.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <param name="outcome">The reported outcome.</param>
    /// <returns>The facts, in declaration order.</returns>
    public static IReadOnlyList<FactKey> Needed(BlockDescriptor descriptor, OutcomeName outcome)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return [.. descriptor.Outputs.Where(o => StepOutputs.CallerWritesOn(o, outcome)).Select(static o => o.Fact)];
    }

    private static string Name(FactKey fact) => fact.Value.StartsWith(FactPrefix, StringComparison.Ordinal) ? fact.Value[FactPrefix.Length..] : fact.Value;
}
