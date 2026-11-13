using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'RecoverAgentNode' does when a node's session ended without a report (issue #1000, PR 7): reads the run directory's output folder and lets <see cref="AgentRecovery.DecideNode"/> decide.
/// A folder that holds every declared output has its files read and checked as 'workflow complete' checks a report; one that holds files but proves nothing parks the node with the folder's path.
/// </summary>
/// <param name="runs">Where the run is read from.</param>
/// <param name="workflows">Where the run's spec is read from.</param>
/// <param name="outputs">The output folder.</param>
internal sealed class AgentNodeRecovering(IWorkflowRunStore runs, IWorkflowStore workflows, IStepOutputs outputs)
{
    /// <summary>Decides the node from its output folder.</summary>
    /// <param name="recover">The node run and its folder.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The report the folder proves, why it proves none, or nothing when the folder is empty.</returns>
    public async Task<AgentNodeRecovered> Recover(AgentNodeRecover recover, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recover);
        var row = GraphFailures.Value(await runs.Get(recover.Node.Run, cancellationToken).ConfigureAwait(false));
        var spec = GraphFailures.Value(await workflows.GetRevision(row.WorkflowId, row.Revision, cancellationToken).ConfigureAwait(false)).Spec;
        var graphNode = spec.Nodes.FirstOrDefault(n => n.Id == recover.Node.Node) ?? throw new ApplicationFailureException($"the spec has no node '{recover.Node.Node.Value}'", nonRetryable: true);
        var declared = RunAgentConfig.DeclaredOutputs(graphNode.Config);
        var names = await outputs.Names(recover.Root, since: null, cancellationToken).ConfigureAwait(false);
        if (names.IsError)
        {
            throw new InvalidOperationException($"{names.Error.Code}: {names.Error.Message}");   // the activity retries: the folder could not be read this time
        }

        if (names.Value.Count == 0)
        {
            return new AgentNodeRecovered(Outputs: null, Reason: null);
        }

        var folder = Path.Combine(recover.Root, AgentRecovery.OutputFolder);
        var decision = AgentRecovery.DecideNode(declared, names.Value, folder);
        return decision.Reason is { } reason ? new AgentNodeRecovered(Outputs: null, reason, await Present(recover.Root, declared, names.Value, cancellationToken).ConfigureAwait(false)) : await Checked(recover.Root, declared, decision.Names, folder, cancellationToken).ConfigureAwait(false);
    }

    // Reads each file and checks the report as 'workflow complete' does; a file that can't be read parks the node.
    private async Task<AgentNodeRecovered> Checked(string root, IReadOnlyDictionary<string, string> declared, IReadOnlyList<string> names, string folder, CancellationToken cancellationToken)
    {
        var reported = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var content = await outputs.Read(root, name, cancellationToken).ConfigureAwait(false);
            if (content.IsError)
            {
                return new AgentNodeRecovered(Outputs: null, $"the agent finished but its result was not recorded: {folder} ({content.Error.Message})", await Present(root, declared, names, cancellationToken).ConfigureAwait(false));
            }

            reported[name] = content.Value;
        }

        var checkedReport = RunAgentReport.Check(declared, reported);
        if (checkedReport.IsError)
        {
            return new AgentNodeRecovered(Outputs: null, $"the agent finished but {checkedReport.Error.Message}: {folder}", await Present(root, declared, names, cancellationToken).ConfigureAwait(false));   // the same check 'complete' applies: the output was never acceptable
        }

        var values = new JsonObject();
        foreach (var (name, value) in checkedReport.Value)
        {
            values[name] = value?.DeepClone();
        }

        return new AgentNodeRecovered(values.ToJsonString(), Reason: null);
    }

    // The declared output files the folder holds, as a JSON object by output name for the failed node's output: a file that parses is its JSON value, else its text. Null when none can be read.
    private async Task<string?> Present(string root, IReadOnlyDictionary<string, string> declared, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        var files = new JsonObject();
        foreach (var name in names.Where(declared.ContainsKey).Order(StringComparer.Ordinal))
        {
            var content = await outputs.Read(root, name, cancellationToken).ConfigureAwait(false);
            if (content.IsError)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(content.Value.Span);
            files[name] = Parsed(text);
        }

        return files.Count == 0 ? null : files.ToJsonString();
    }

    private static JsonNode? Parsed(string text)
    {
        try
        {
            return JsonNode.Parse(text) ?? JsonValue.Create(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text.TrimEnd('\n'));
        }
    }
}
