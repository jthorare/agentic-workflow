using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's logic (<see cref="OpsEditor"/>, continued): a step the person is adding. Its form is made from the block, and nothing reaches the server until Save sends one add_node.</summary>
internal sealed partial class OpsEditor
{
    private PendingStep? _pending;

    // A step the person is filling in: nothing of it exists on the server until Save sends the add_node.
    private sealed record PendingStep(string Id, string Type, string? After);

    /// <summary>True while the form is for a step that does not exist yet.</summary>
    internal bool HasPendingStep => _pending is not null;

    /// <summary>True when every required field of the form has text.</summary>
    internal bool RequiredFilled => Fields.All(f => !f.IsRequired || !string.IsNullOrWhiteSpace(f.Text));

    /// <summary>Makes the form of a step that does not exist yet, from its block's config fields; nothing is sent.</summary>
    /// <param name="block">The block picked.</param>
    /// <param name="id">The id the new node gets.</param>
    /// <param name="after">The node the new one follows, or null for none.</param>
    internal void BeginNewStep(WorkflowBlock block, string id, string? after)
    {
        _pending = new PendingStep(id, block.Type, after);
        Fields.Clear();
        var example = block.ExampleConfig ?? default;   // the block's example settings: a step that opens complete saves untouched; a block with no example opens with its required fields empty
        foreach (var field in block.Config)
        {
            var boolean = string.Equals(field.Type, "boolean", StringComparison.Ordinal);
            var text = EditorFieldText.Of(example, field);
            Fields.Add(new ConfigFieldViewModel(field, text.Length == 0 && boolean && field.Required ? "false" : text));   // why: a required check box shows a value, and the value it shows is the one sent
        }

        NodeHeading = Labels.WfEditorNewStep(block.Title);

        OnChanged(nameof(HasPendingStep));
    }

    /// <summary>Drops the new step being filled in, with whatever was typed.</summary>
    internal void ClearPendingStep()
    {
        if (_pending is null)
        {
            return;
        }

        _pending = null;
        Fields.Clear();
        NodeHeading = string.Empty;
        OnChanged(nameof(HasPendingStep));
    }

    /// <summary>Sends the new step as ONE add_node with the filled config (and a connect from the node it follows). A field with a problem sends nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the server applied the edit.</returns>
    internal async Task<bool> AddPendingStep(CancellationToken cancellationToken)
    {
        if (_pending is not { } step)
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

        var add = new JsonObject { ["op"] = "add_node", ["id"] = step.Id, ["type"] = step.Type };
        if (config.Count > 0)
        {
            add["config"] = config;
        }

        var ops = step.After is null
            ? new JsonArray((JsonNode)add)
            : new JsonArray(add, new JsonObject { ["op"] = "connect", ["from"] = step.After, ["to"] = step.Id });
        return await Send(ops, cancellationToken).ConfigureAwait(true);
    }
}
