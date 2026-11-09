using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete': a 'run_agent' node's report (PR 7). The first argument is the run's id and '--run' the node's token on the wire (<see cref="RunAgentWire"/>); each '--output fact:&lt;field&gt;'
/// is one declared output. The service finds the node, decides who may report it (<see cref="AgentReportAuthority.DecideNode"/>), checks the report against the node's declared outputs
/// (<see cref="RunAgentReport"/>: a missing or mistyped field is refused by name and the agent may report again) and hands the values to the run's workflow, which ends the node.
/// </summary>
/// <param name="runs">Where the run is read from.</param>
/// <param name="workflows">Where the run's spec is read from.</param>
/// <param name="control">The run's workflow: which node it waits on, and the report.</param>
/// <param name="files">The launches' stored secrets.</param>
internal sealed class CompleteStepService(IWorkflowRunStore runs, IWorkflowStore workflows, IAgentNodeControl control, ILaunchFiles files) : IService<CompleteStepRequest, CompleteStepResponse>
{
    private const string Done = "done";
    private const string FactPrefix = "fact:";

    public async ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WorkflowRunId.TryFrom(request.Instance, out var run))
        {
            return SindriErrors.InvalidInput.With("the first argument is the run's id, such as 'contract-drift@3:12' (fix: copy the 'Report with:' line of the node's brief)");
        }

        var found = await Find(run, request.Run, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var (node, itemIndex) = found.Value;
        var runtime = await Authorize(run, (node, itemIndex), request, cancellationToken).ConfigureAwait(false);
        if (runtime.IsError)
        {
            return runtime.Error;
        }

        var outputs = Outputs(request, node);
        if (outputs.IsError)
        {
            return outputs.Error;
        }

        var delivered = await control.Report(run, new AgentNodeReport(node.Id, itemIndex, outputs.Value, Human: runtime.Value == StepRuntime.Desk), cancellationToken).ConfigureAwait(false);
        return delivered.IsError ? delivered.Error : new CompleteStepResponse($"-> {node.Id.Value} done", request.Nonce ?? NonceOf(run, node.Id, itemIndex), AlreadyApplied: false);
    }

    // Who reports: the launched session with its secret, or the human of a taken-over node. A report for a node that is not running is refused whoever sends it.
    private async ValueTask<Result<StepRuntime>> Authorize(WorkflowRunId run, (GraphNode Node, int ItemIndex) named, CompleteStepRequest request, CancellationToken cancellationToken)
    {
        var running = await control.Running(run, cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var view = running.Value is { } now && now.Node == named.Node.Id && now.ItemIndex == named.ItemIndex ? new NodeReportView(now.Launch, now.TakenOver) : null;
        var runtime = AgentReportAuthority.DecideNode(view, request, files);
        return runtime.IsSuccess && view is null
            ? SindriErrors.InvalidInput.With($"node '{named.Node.Id.Value}' of run '{run.Value}' is not running, so it takes no report")
            : runtime;
    }

    private async ValueTask<Result<(GraphNode Node, int ItemIndex)>> Find(WorkflowRunId run, RunToken token, CancellationToken cancellationToken)
    {
        var row = await runs.Get(run, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var revision = await workflows.GetRevision(row.Value.WorkflowId, row.Value.Revision, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        return RunAgentWire.NodeOf(revision.Value.Spec, token) is { } named && string.Equals(named.Node.Type, "run_agent", StringComparison.Ordinal)
            ? (named.Node, named.ItemIndex)
            : SindriErrors.InvalidInput.With($"run '{run.Value}' has no run_agent node for token '{token.Value}' (fix: use the token the node's brief shows)");
    }

    // The checked values of the declared outputs as a JSON object: the outcome must be 'done', each output 'fact:<field>', and the report whole (RunAgentReport).
    private static Result<string> Outputs(CompleteStepRequest request, GraphNode node)
    {
        if (!string.Equals(request.Outcome.Value, Done, StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"a run_agent node has one outcome, '{Done}' (fix: --outcome {Done})");
        }

        var reported = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var (fact, content) in request.Outputs)
        {
            if (!fact.Value.StartsWith(FactPrefix, StringComparison.Ordinal))
            {
                return SindriErrors.InvalidInput.With($"'{fact.Value}' is not an output of a run_agent node; outputs are 'fact:<field>' (fix: --output fact:<field>=<file>)");
            }

            reported[fact.Value[FactPrefix.Length..]] = content;
        }

        var checkedReport = RunAgentReport.Check(RunAgentConfig.DeclaredOutputs(node.Config), reported);
        if (checkedReport.IsError)
        {
            return checkedReport.Error;
        }

        var values = new JsonObject();
        foreach (var (name, value) in checkedReport.Value)
        {
            values[name] = value?.DeepClone();
        }

        return values.ToJsonString();
    }

    // why: a report that names no nonce gets one made from the node run, so a repeat of the same report is the same command
    private static CommandNonce NonceOf(WorkflowRunId run, NodeId node, int itemIndex) =>
        CommandNonce.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"report:{run.Value}:{node.Value}:{itemIndex}"))))[..32]);
}
