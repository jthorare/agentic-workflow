using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The workflow editor's logic (#2258 PR 17 part 2b), no view yet: it reads a workflow and the block list, builds the form of the selected node from its block's config fields, and
/// sends each edit as ops with the revision it read as the base. Each edit is sent once and never retried; a workflow that changed elsewhere is not overwritten but asks for a reload.
/// </summary>
internal sealed class OpsEditor(ISindriApi api) : ObservableObject
{
    private const string StaleBase = "SND-GRAPH-007";

    private Uri? _url;
    private int _reads;
    private string? _selected;

    /// <summary>The workflow's slug; empty until loaded.</summary>
    public string Slug { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The newest revision read: the base every edit is sent on.</summary>
    public int BaseRevision { get; private set => Set(ref field, value); }

    /// <summary>The nodes of the workflow as read.</summary>
    public IReadOnlyList<EditorNode> Nodes { get; private set => Set(ref field, value); } = [];

    /// <summary>The block types by group, as the picker shows them.</summary>
    public IReadOnlyList<WorkflowBlockGroup> Groups { get; private set => Set(ref field, value); } = [];

    /// <summary>The form of the selected node.</summary>
    public ObservableCollection<ConfigFieldViewModel> Fields { get; } = [];

    /// <summary>True when the selected step has settings in an Advanced group.</summary>
    public bool HasAdvancedFields => Fields.Any(f => f.InAdvanced);

    /// <summary>True when the Advanced group is open.</summary>
    public bool AdvancedOpen
    {
        get;
        set
        {
            Set(ref field, value);
            foreach (var f in Fields)
            {
                f.GroupOpen = value;
            }
        }
    }

    /// <summary>The Advanced toggle's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string AdvancedText => Labels.WfFieldAdvanced;
#pragma warning restore CA1822, MA0041

    /// <summary>The selected step as the sheet names it ("Every weekday at 7:00 \u00b7 trigger"), never its id; empty when none is selected.</summary>
    public string NodeHeading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while an edit is on the way; a second edit does nothing.</summary>
    public bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>True when the workflow changed elsewhere: edits are held back until Reload.</summary>
    public bool NeedsReload { get; private set => Set(ref field, value); }

    /// <summary>The prompt under the editor when the workflow changed elsewhere; empty otherwise.</summary>
    public string ReloadPromptText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Holds every edit until a reload: the workflow changed since the page read it.</summary>
    internal void MarkChangedElsewhere() => (NeedsReload, ReloadPromptText) = (true, Labels.WfEditChangedElsewhere);

    /// <summary>The Reload command's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ReloadText => Labels.WfReload;
#pragma warning restore CA1822, MA0041

    /// <summary>Why the last edit or read failed (not a stale base), in words; empty otherwise.</summary>
    public string FailureText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Drops the refusal on screen: it belongs to the sheet it came from, which is closing or moving on.</summary>
    internal void ClearFailure() => FailureText = string.Empty;

    /// <summary>Reads a workflow and the block list. Both must be read: the form is made from the two.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when both are read.</returns>
    internal Task Load(Uri url, string slug, CancellationToken cancellationToken) => Load(url, slug, afterSave: false, cancellationToken);

    private async Task Load(Uri url, string slug, bool afterSave, CancellationToken cancellationToken)
    {
        if (!string.Equals(Slug, slug, StringComparison.Ordinal))
        {
            // another workflow: nothing of the old one stays to be saved to this one, even when this read fails
            Fields.Clear();
            (Nodes, BaseRevision, _selected, NodeHeading) = ([], 0, null, string.Empty);
        }

        (_url, Slug) = (url, slug);
        var read = ++_reads;
        var workflowRead = api.Workflow(url, slug, cancellationToken);
        var blocksRead = api.WorkflowBlocks(url, cancellationToken);
        await Task.WhenAll(workflowRead, blocksRead).ConfigureAwait(true);
        var (workflow, blocks) = (await workflowRead.ConfigureAwait(true), await blocksRead.ConfigureAwait(true));
        if (read != _reads)
        {
            return;   // a newer read was asked for since
        }

        if (workflow.IsError || blocks.IsError)
        {
            var error = workflow.IsError ? workflow.Error : blocks.Error;
            FailureText = afterSave ? Labels.WfEditSavedReloadFailed(error) : Labels.WfEditLoadFailed(error);
            return;
        }

        (BaseRevision, Nodes, Groups) = (workflow.Value.LatestRevision, ReadNodes(workflow.Value.Spec), blocks.Value.Groups);
        (NeedsReload, ReloadPromptText, FailureText) = (false, string.Empty, string.Empty);
        Select(_selected ?? string.Empty);   // the form stays on its node, with what the new revision holds
    }

    /// <summary>Reads the workflow again (the Reload command): the held edit is dropped, never retried.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the workflow is read.</returns>
    internal Task Reload(CancellationToken cancellationToken) => _url is { } url ? Load(url, Slug, cancellationToken) : Task.CompletedTask;

    /// <summary>Builds the form of a node from its block's config fields.</summary>
    /// <param name="nodeId">The node.</param>
    internal void Select(string nodeId)
    {
        _selected = nodeId.Length == 0 ? null : nodeId;
        FailureText = string.Empty;   // a refusal belongs to the step it came from
        Fields.Clear();
        var node = Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.Ordinal));
        var block = node is null ? null : Groups.SelectMany(g => g.Blocks).FirstOrDefault(b => string.Equals(b.Type, node.Type, StringComparison.Ordinal));
        NodeHeading = HeadingOf(node, block);
        foreach (var field in block?.Config ?? [])
        {
            Fields.Add(new ConfigFieldViewModel(field, EditorFieldText.Of(node!.Config, field), block!.Type, ExampleOf(block, field.Name)));   // why: a block was found, so node and block are not null
        }

        AdvancedOpen = false;
        OnChanged(nameof(HasAdvancedFields));
    }

    private static string ExampleOf(WorkflowBlock block, string field) =>
        block.ExampleConfig is { ValueKind: JsonValueKind.Object } example && example.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && string.Equals(field, "code", StringComparison.Ordinal)
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string HeadingOf(EditorNode? node, WorkflowBlock? block)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var title = node.Title is { Length: > 0 } own ? own : block?.Title ?? node.Type;
        return Labels.WfEditorStep(title, block is null ? node.Type : block.IsTrigger ? Labels.WfEditorTriggerKind : block.Title);
    }

    /// <summary>Sends a set_config for one field of the selected node.</summary>
    /// <param name="name">The field.</param>
    /// <param name="text">The new text; empty on an optional field removes the key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task EditField(string name, string text, CancellationToken cancellationToken)
    {
        if (_selected is not { } node || Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal)) is not { } field)
        {
            return;
        }

        var (value, problem) = EditorFieldText.Parse(field, text);
        field.ErrorText = problem;
        if (problem.Length == 0)
        {
            await Send(new JsonArray((JsonNode)new JsonObject { ["op"] = "set_config", ["node"] = node, ["path"] = name, ["value"] = value }), cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Sends a set_config for each of several fields of the selected node as ONE call on the revision read: all are applied or none. Every text is checked first; a problem on any field sends nothing.</summary>
    /// <param name="edits">The fields and their new text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task EditFields(IReadOnlyList<(string Name, string Text)> edits, CancellationToken cancellationToken)
    {
        if (_selected is not { } node)
        {
            return Task.CompletedTask;
        }

        var ops = new JsonArray();
        var blocked = false;
        foreach (var (name, text) in edits)
        {
            if (Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal)) is not { } field)
            {
                continue;
            }

            var (value, problem) = EditorFieldText.Parse(field, text);
            field.ErrorText = problem;
            blocked |= problem.Length > 0;
            ops.Add((JsonNode)new JsonObject { ["op"] = "set_config", ["node"] = node, ["path"] = name, ["value"] = value });
        }

        return blocked || ops.Count == 0 ? Task.CompletedTask : Send(ops, cancellationToken);
    }

    /// <summary>Sends an add_node.</summary>
    /// <param name="id">The new node's id.</param>
    /// <param name="type">Its block type.</param>
    /// <param name="title">Its title, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task AddNode(string id, string type, string? title, CancellationToken cancellationToken)
    {
        var op = new JsonObject { ["op"] = "add_node", ["id"] = id, ["type"] = type };
        if (title is not null)
        {
            op["title"] = title;
        }

        return Send(new JsonArray((JsonNode)op), cancellationToken);
    }

    /// <summary>Sends an add_node and, when a node is given, a connect from it to the new one, as one set of ops: both are saved or neither.</summary>
    /// <param name="id">The new node's id.</param>
    /// <param name="type">Its block type.</param>
    /// <param name="after">The node the new one follows, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task AddNodeAfter(string id, string type, string? after, CancellationToken cancellationToken)
    {
        var add = new JsonObject { ["op"] = "add_node", ["id"] = id, ["type"] = type };
        var ops = after is null
            ? new JsonArray((JsonNode)add)
            : new JsonArray(add, new JsonObject { ["op"] = "connect", ["from"] = after, ["to"] = id });

        return Send(ops, cancellationToken);
    }

    /// <summary>Sends a connect.</summary>
    /// <param name="from">The node the edge leaves.</param>
    /// <param name="to">The node it enters.</param>
    /// <param name="output">The branch it leaves by, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Connect(string from, string to, string? output, CancellationToken cancellationToken)
    {
        var op = new JsonObject { ["op"] = "connect", ["from"] = from, ["to"] = to };
        if (output is not null)
        {
            op["output"] = output;
        }

        return Send(new JsonArray((JsonNode)op), cancellationToken);
    }

    /// <summary>Sends a remove_node.</summary>
    /// <param name="id">The node.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RemoveNode(string id, CancellationToken cancellationToken) =>
        Send(new JsonArray((JsonNode)new JsonObject { ["op"] = "remove_node", ["node"] = id }), cancellationToken);

    // One call, on the revision read, never retried. A saved edit reads the workflow again; a stale base asks for a reload and holds further edits.
    private async Task Send(JsonArray ops, CancellationToken cancellationToken)
    {
        if (IsSending || NeedsReload || _url is not { } url || Slug.Length == 0)
        {
            return;
        }

        FailureText = string.Empty;
        IsSending = true;
        try
        {
            var applied = await api.ApplyWorkflowOps(url, Slug, BaseRevision, ops.ToJsonString(), cancellationToken).ConfigureAwait(true);
            if (applied.IsError)
            {
                Refused(applied.Error);
                return;
            }

            await Load(url, Slug, afterSave: true, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsSending = false;
        }
    }

    private void Refused(Error error)
    {
        if (string.Equals(error.Code, StaleBase, StringComparison.Ordinal))
        {
            (NeedsReload, ReloadPromptText) = (true, Labels.WfEditChangedElsewhere);
            return;
        }

        var unsure = string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal);
        FailureText = unsure ? Labels.WfEditUnsure(error) : Labels.WfEditRefused(error);
    }

    private static List<EditorNode> ReadNodes(JsonElement spec)
    {
        var nodes = new List<EditorNode>();
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty("nodes", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return nodes;
        }

        foreach (var node in list.EnumerateArray())
        {
            var title = node.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            var config = node.TryGetProperty("config", out var c) ? c.Clone() : default;
            nodes.Add(new EditorNode(node.GetProperty("id").GetString() ?? string.Empty, node.GetProperty("type").GetString() ?? string.Empty, title, config));
        }

        return nodes;
    }
}
