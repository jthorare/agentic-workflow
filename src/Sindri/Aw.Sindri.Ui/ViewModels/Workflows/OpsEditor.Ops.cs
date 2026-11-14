using System.Text.Json.Nodes;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's graph ops (<see cref="OpsEditor"/>, continued): add a step, connect two, remove one. Each is one call through <c language="text">Send</c>, on the revision read.</summary>
internal sealed partial class OpsEditor
{
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
}
