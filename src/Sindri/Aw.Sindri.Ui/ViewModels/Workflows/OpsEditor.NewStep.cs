using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's logic (<see cref="OpsEditor"/>, continued): the form of a step that does not exist yet. Save sends one add_node; Cancel sends nothing.</summary>
internal sealed partial class OpsEditor
{
    /// <summary>True while the form is that of a step not added yet: Save sends one add_node, Cancel sends nothing.</summary>
    public bool IsNewStep { get; private set => Set(ref field, value); }

    /// <summary>The block of the step being made, or null.</summary>
    internal WorkflowBlock? PendingBlock { get; private set; }

    /// <summary>Opens the form of a step that does not exist yet: the block's fields, empty. Nothing is sent until <see cref="SaveNew"/>.</summary>
    /// <param name="block">The block the person picked.</param>
    internal void BeginNew(WorkflowBlock block)
    {
        _selected = null;
        Fields.Clear();
        foreach (var field in block.Config)
        {
            Fields.Add(new ConfigFieldViewModel(field, string.Empty));
        }

        NodeHeading = StepKinds.Name(block.Type, block.Title);
        (PendingBlock, IsNewStep) = (block, true);
    }

    /// <summary>Closes the form of the step being made; nothing was sent, so nothing is undone.</summary>
    internal void CancelNew()
    {
        if (!IsNewStep)
        {
            return;
        }

        (PendingBlock, IsNewStep) = (null, false);
        Fields.Clear();
        NodeHeading = string.Empty;
    }

    /// <summary>Sends the step being made as ONE call: an add_node with the filled config and, when a node is given, a connect from it. A required field left empty is refused in its field and nothing is sent.</summary>
    /// <param name="id">The new node's id.</param>
    /// <param name="after">The node the new one follows, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the workflow took the step.</returns>
    internal async Task<bool> SaveNew(string id, string? after, CancellationToken cancellationToken)
    {
        if (PendingBlock is not { } block)
        {
            return false;
        }

        var config = new JsonObject();
        var blocked = false;
        foreach (var field in Fields)
        {
            var (value, problem) = EditorFieldText.Parse(field, field.Text);
            field.ErrorText = problem;
            blocked |= problem.Length > 0;
            if (value is not null)
            {
                config[field.Name] = value;
            }
        }

        if (blocked)
        {
            return false;
        }

        var add = new JsonObject { ["op"] = "add_node", ["id"] = id, ["type"] = block.Type };
        if (config.Count > 0)
        {
            add["config"] = config;
        }

        var ops = after is null
            ? new JsonArray((JsonNode)add)
            : new JsonArray(add, new JsonObject { ["op"] = "connect", ["from"] = after, ["to"] = id });
        return await Send(ops, cancellationToken, ClearPending).ConfigureAwait(true);
    }

    private void ClearPending() => (PendingBlock, IsNewStep) = (null, false);
}
