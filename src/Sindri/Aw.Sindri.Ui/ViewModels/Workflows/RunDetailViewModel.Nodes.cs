using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The run page's view model (<see cref="RunDetailViewModel"/>, continued): what a read of the run is turned into: its node types and statuses, its trigger's word, and the failure callout.</summary>
internal sealed partial class RunDetailViewModel
{
    // The served word tells a CLI or agent start from one by hand (#3683).
    private static string? TriggerKindOf(WorkflowRunView run) => run.TriggerKind is { Length: > 0 } served ? served : KindOf(run.Trigger);

    // The failure callout of a run that failed, on the step its error names; null for a run with nothing to say.
    private RunFailureViewModel? FailureOf(WorkflowRunView run)
    {
        var failedNode = FailedNode(run);
        return failedNode?.Failure is { } failed ? new RunFailureViewModel(failed, run.Error ?? failed.Detail, clipboard, clock, failedNode.Node) : null;
    }

    // The block type of each node of the revision the page read; a run of another revision may name a node by another type, so only the read revision's types are used.
    private static Dictionary<string, string> NodeTypes(WorkflowView? shape)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        if (shape?.Spec is { ValueKind: JsonValueKind.Object } spec && spec.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.TryGetProperty("id", out var id) && node.TryGetProperty("type", out var type) && id.GetString() is { } key && type.GetString() is { } kind)
                {
                    types[key] = kind;
                }
            }
        }

        return types;
    }

    // a node run per item of a for_each: the last one served speaks for the node
    private static Dictionary<string, string> Statuses(WorkflowRunView run)
    {
        var statuses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in run.Nodes)
        {
            statuses[node.Node] = node.Status;
        }

        return statuses;
    }

    // The trigger is the stored object; its "kind" names it. One without a kind reads as "an unknown trigger" rather than failing the run.
    private static string? KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is { Length: > 0 } text
            ? text
            : null;

    // The callout is for a run that failed: a step that failed and went on (onFailure 'continue' or 'skip-item') leaves a failed row in a run that succeeded or is still going.
    // When several rows failed, the one the run's own error names ("node 'check' failed: ...") is the step the run stopped at, else the last.
    private static NodeRunView? FailedNode(WorkflowRunView run)
    {
        if (string.IsNullOrEmpty(run.Error))
        {
            return null;
        }

        var failed = run.Nodes.Where(static n => n.Failure is not null).ToList();
        return failed.LastOrDefault(n => run.Error.Contains($"node '{n.Node}'", StringComparison.Ordinal)) ?? failed.LastOrDefault();
    }
}
