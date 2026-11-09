using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run with every node's status, input, output and error. Nodes keep the order they were served in, grouped by node id.</summary>
internal sealed class RunDetailViewModel(ISindriApi api, TimeProvider clock) : ObservableObject
{
    private int _reads;

    /// <summary>The run's id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Run morning-check@1:1".</summary>
    public string TitleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The status: glyph and word.</summary>
    public string ChipText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started by manual".</summary>
    public string TriggerText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Revision 1".</summary>
    public string RevisionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"Started 4m ago" while it is live, "Ended 5m ago" after.</summary>
    public string WhenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why the run failed, as served; empty otherwise.</summary>
    public string ErrorText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the run has an error.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The run's outputs as indented JSON; empty while none.</summary>
    public string OutputsText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the run has outputs.</summary>
    public bool HasOutputs => OutputsText.Length > 0;

    /// <summary>The heading over the outputs.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string OutputsHeading => Labels.WfOutputsHeading;

    /// <summary>"Reading the run…" until the first answer.</summary>
    public string ReadingText => Labels.WfRunReading;
#pragma warning restore CA1822, MA0041

    /// <summary>The nodes, grouped by id, in the order first served.</summary>
    public IReadOnlyList<NodeGroupViewModel> Groups { get; private set => Set(ref field, value); } = [];

    /// <summary>True until the answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); }

    /// <summary>True when the read failed.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Reads one run and shows it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the run is shown or the failure is.</returns>
    internal async Task Read(Uri url, string runId, CancellationToken cancellationToken)
    {
        var read = ++_reads;
        if (!string.Equals(Id, runId, StringComparison.Ordinal))
        {
            Id = runId;
            Groups = [];   // another run: nothing of the old one stays
            (TitleText, ChipText, TriggerText, RevisionText, WhenText, ErrorText, OutputsText) = (Labels.WfRunTitle(runId), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            Changed();
        }

        IsReading = Groups.Count == 0;
        var run = await api.WorkflowRun(url, runId, cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read was asked for since
        }

        IsReading = false;
        if (run.IsError)
        {
            (IsFailed, FailedText) = (true, Labels.WfRunFailed(run.Error));
            return;
        }

        (IsFailed, FailedText) = (false, string.Empty);
        Show(run.Value);
    }

    private void Show(WorkflowRunView run)
    {
        var (glyph, word) = Labels.WfChip(run.Status);
        TitleText = Labels.WfRunTitle(run.Id);
        ChipText = glyph + " " + word;
        TriggerText = Labels.WfStartedBy(KindOf(run.Trigger));
        RevisionText = Labels.WfRunRevision(run.Revision);
        var live = string.Equals(run.Status, "running", StringComparison.Ordinal) || string.Equals(run.Status, "waiting", StringComparison.Ordinal) || string.Equals(run.Status, "queued", StringComparison.Ordinal);
        WhenText = live
            ? Labels.JobWhen(running: true, clock.GetUtcNow() - run.StartedAt)
            : Labels.JobWhen(running: false, clock.GetUtcNow() - (run.EndedAt ?? run.StartedAt));
        ErrorText = run.Error ?? string.Empty;
        OutputsText = WorkflowJson.Pretty(run.Outputs);
        Groups = [.. run.Nodes.GroupBy(n => n.Node, StringComparer.Ordinal).Select(g => new NodeGroupViewModel(g.Key, [.. g]))];
        Changed();
    }

    private void Changed()
    {
        OnChanged(nameof(HasError));
        OnChanged(nameof(HasOutputs));
    }

    // The trigger is the stored object; its "kind" names it. One without a kind reads as an unknown trigger rather than failing the run.
    private static string KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() is { Length: > 0 } text
            ? text
            : Labels.WfUnknownTrigger;
}
