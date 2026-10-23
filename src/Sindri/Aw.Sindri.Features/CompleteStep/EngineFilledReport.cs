using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>What 'workflow complete' knows about the outputs the engine fills (DT spec §8.1): which facts those are, from the build's descriptors, and how a decision is written.</summary>
/// <param name="catalog">The build's descriptors.</param>
/// <param name="json">Writes the engine-filled facts.</param>
/// <param name="launchFiles">The launches' stored secrets, which prove an agent session's report.</param>
/// <param name="outputTypes">Checks an agent's outputs against the types its recipe declares.</param>
internal sealed class EngineFilledReport(BlockCatalog catalog, JsonSerializerOptions json, ILaunchFiles launchFiles, ReportOutputTypes outputTypes)
{
    /// <summary>The refusal for a report that writes an engine-filled output itself (SND-CLI-003), checked without reading the instance.</summary>
    /// <param name="written">The output keys the caller gave.</param>
    /// <returns>The refusal; null when none of them is engine-filled.</returns>
    public Error? Refusal(IEnumerable<FactKey> written) =>
        EngineFilledOutputs.Refusal(catalog.Blocks, written) is { } message ? SindriErrors.InvalidInput.With(message) : null;

    /// <summary>
    /// The refusal (SND-AUTH-002) for a non-human caller reporting on a human-only Step (DT spec §8.5, scoping-station spec W3): the
    /// open task is this run's and <see cref="HumanOnlySteps"/> says a human must report it.
    /// </summary>
    /// <param name="view">The instance as read.</param>
    /// <param name="request">The report.</param>
    /// <param name="caller">Who reports.</param>
    /// <returns>The refusal; null when the report may go on.</returns>
    public Error? HumanOnlyRefusal(InstanceView view, CompleteStepRequest request, CallerContext caller)
    {
        if (caller.Class == CallerClass.Human || view.Task is not { } task || task.Run != request.Run || catalog.Find(task.Step) is not { } step || !HumanOnlySteps.Applies(step, StepRuntime.Desk))
        {
            return null;
        }

        return SindriErrors.AuthRefused.With(
            $"CompleteStep on '{task.Step.Value}' (state '{task.State}') is a human-only Step. Ask the human to run it in a terminal or the Sindri app: {CompleteCommand.Line(view.Address.Value, request.Run, [request.Outcome], task.Outputs)}");
    }

    /// <summary>Who may report the run (see <see cref="AgentReportAuthority"/>): a non-human caller's report on an agent run is proven by its launch secret.</summary>
    /// <param name="view">The instance as read.</param>
    /// <param name="request">The report.</param>
    /// <returns>The runtime the report is recorded as, or the refusal.</returns>
    public Result<StepRuntime> Authorize(InstanceView view, CompleteStepRequest request) => AgentReportAuthority.Decide(view, request, launchFiles);

    /// <summary>The refusal for an agent's output that isn't the type its recipe declares (see <see cref="ReportOutputTypes"/>).</summary>
    /// <param name="view">The instance as read.</param>
    /// <param name="request">The report.</param>
    /// <param name="cancellationToken">Cancels the recipe read.</param>
    /// <returns>The refusal; null when every declared output holds.</returns>
    public ValueTask<Error?> OutputTypeRefusal(InstanceView view, CompleteStepRequest request, CancellationToken cancellationToken) => outputTypes.Check(view, request, cancellationToken);

    /// <summary>The caller's outputs and the engine-filled ones, written.</summary>
    /// <param name="given">The caller's outputs.</param>
    /// <param name="filled">The engine-filled decisions.</param>
    /// <returns>Every output's content.</returns>
    public Dictionary<FactKey, ReadOnlyMemory<byte>> Merge(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> given, IReadOnlyDictionary<FactKey, DecisionFact> filled)
    {
        var merged = new Dictionary<FactKey, ReadOnlyMemory<byte>>(given);
        foreach (var (fact, decision) in filled)
        {
            merged[fact] = AwJson.Serialize(decision, json);
        }

        return merged;
    }
}
