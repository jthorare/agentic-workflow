using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>The working copy of a graph while a batch of ops applies: each op changes it or returns why it can't.</summary>
internal sealed class GraphEditSession(GraphSpec spec, Func<string, bool> isTriggerType)
{
    private const string DefaultOutput = "out";

    private readonly List<GraphNode> _nodes = [.. spec.Nodes.Select(static n => n with { Config = (JsonObject)n.Config.DeepClone() })];
    private readonly List<GraphEdge> _edges = [.. spec.Edges];
    private string _name = spec.Name;
    private string? _description = spec.Description;
    private string? _concurrency = spec.Concurrency;

    public GraphSpec ToSpec() => spec with { Name = _name, Description = _description, Concurrency = _concurrency, Nodes = [.. _nodes], Edges = [.. _edges] };

    // why: returns the reason an op can't apply, or null once it is applied; the caller adds the op's number.
    public string? Apply(GraphOp op) => op switch
    {
        AddNodeOp add => Add(add),
        SetConfigOp set => SetConfig(set),
        RemoveNodeOp remove => Remove(remove.Node.Value),
        ConnectOp connect => Connect(connect),
        DisconnectOp disconnect => Disconnect(disconnect),
        RenameOp rename => Rename(rename),
        SetMetaOp meta => Meta(meta),
        ReplaceNodeOp replace => Replace(replace),
        _ => "that is not a known op",
    };

    // why: returns the start of the SND-GRAPH-112 sentence when a replace_node names a node that is not the trigger, or null (a missing node is the op's own problem)
    public string? NotTheTrigger(GraphOp op) =>
        op is ReplaceNodeOp replace && IndexOf(replace.Node.Value) is var at and >= 0 && !isTriggerType(_nodes[at].Type) ? $"'{replace.Node.Value}' is not the trigger." : null;

    private string? Replace(ReplaceNodeOp replace)
    {
        var index = IndexOf(replace.Node.Value);
        if (index < 0)
        {
            return $"there is no node '{replace.Node.Value}'";
        }

        if (!isTriggerType(replace.Type))
        {
            return $"'{replace.Type}' is not a trigger, so it can't take the trigger's place";
        }

        var old = _nodes[index];
        _nodes[index] = old with { Type = replace.Type, Title = replace.Title ?? old.Title, Config = CloneConfig(replace.Config) };
        return null;
    }

    private static JsonObject CloneConfig(JsonObject? config)
    {
        var copy = new JsonObject();
        if (config is null)
        {
            return copy;
        }

        foreach (var (key, value) in config)
        {
            copy[key] = value?.DeepClone();
        }

        return copy;
    }

    private int IndexOf(string id) => _nodes.FindIndex(n => string.Equals(n.Id.Value, id, StringComparison.Ordinal));

    private string? Add(AddNodeOp add)
    {
        if (IndexOf(add.Id.Value) >= 0)
        {
            return $"node '{add.Id.Value}' already exists";
        }

        _nodes.Add(new GraphNode(add.Id, add.Type, add.Title, CloneConfig(add.Config), add.OnFailure));
        return null;
    }

    private string? SetConfig(SetConfigOp set)
    {
        var index = IndexOf(set.Node.Value);
        if (index < 0)
        {
            return $"there is no node '{set.Node.Value}'";
        }

        var keys = set.Path.Split('.');
        if (keys.Any(static k => k.Length == 0))
        {
            return $"path '{set.Path}' needs keys separated by single dots, such as 'output.type'";
        }

        JsonNode holder = _nodes[index].Config;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var (next, problem) = Step(holder, keys[i]);
            if (next is null)
            {
                return problem ?? $"the path can't go through '{keys[i]}'";
            }

            holder = next;
        }

        return Put(holder, keys[^1], set.Value?.DeepClone());
    }

    // why: a missing object on the path is created; anything else that isn't an object or a list item is a path error, not a silent overwrite.
    private static (JsonNode? Next, string? Problem) Step(JsonNode holder, string key)
    {
        switch (holder)
        {
            case JsonObject obj:
                if (obj[key] is { } existing)
                {
                    return existing is JsonObject or JsonArray ? (existing, null) : (null, $"'{key}' holds a value, not an object, so the path can't go through it");
                }

                var made = new JsonObject();
                obj[key] = made;
                return (made, null);
            case JsonArray list when int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var at) && at < list.Count:
                return list[at] is JsonObject or JsonArray ? (list[at], null) : (null, $"item {key} holds a value, not an object, so the path can't go through it");
            default:
                return (null, $"'{key}' is not a key of an object or an item of a list");
        }
    }

    private static string? Put(JsonNode holder, string key, JsonNode? value)
    {
        switch (holder)
        {
            case JsonObject obj:
                _ = value is null ? obj.Remove(key) : SetKey(obj, key, value);
                return null;
            case JsonArray list when int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var at) && at < list.Count:
                if (value is null)
                {
                    list.RemoveAt(at);
                }
                else
                {
                    list[at] = value;
                }

                return null;
            default:
                return $"'{key}' is not a key of an object or an item of a list";
        }
    }

    private static bool SetKey(JsonObject obj, string key, JsonNode value)
    {
        obj[key] = value;
        return true;
    }

    private string? Remove(string id)
    {
        var index = IndexOf(id);
        if (index < 0)
        {
            return $"there is no node '{id}'";
        }

        _nodes.RemoveAt(index);
        _ = _edges.RemoveAll(e => string.Equals(e.From.Value, id, StringComparison.Ordinal) || string.Equals(e.To.Value, id, StringComparison.Ordinal));
        return null;
    }

    private static bool Same(GraphEdge edge, NodeId from, string? output, NodeId to) =>
        edge.From.Value == from.Value && edge.To.Value == to.Value && string.Equals(edge.Output ?? DefaultOutput, output ?? DefaultOutput, StringComparison.Ordinal);

    private string? Ends(NodeId from, NodeId to) =>
        IndexOf(from.Value) < 0 ? $"there is no node '{from.Value}'" : IndexOf(to.Value) < 0 ? $"there is no node '{to.Value}'" : null;

    private string? Connect(ConnectOp connect)
    {
        if (Ends(connect.From, connect.To) is { } problem)
        {
            return problem;
        }

        if (_edges.Exists(e => Same(e, connect.From, connect.Output, connect.To)))
        {
            return $"the edge '{connect.From.Value}' -> '{connect.To.Value}' already exists";
        }

        _edges.Add(new GraphEdge(connect.From, connect.Output, connect.To));
        return null;
    }

    private string? Disconnect(DisconnectOp disconnect)
    {
        var index = _edges.FindIndex(e => Same(e, disconnect.From, disconnect.Output, disconnect.To));
        if (index < 0)
        {
            return $"there is no edge '{disconnect.From.Value}' -> '{disconnect.To.Value}' to remove";
        }

        _edges.RemoveAt(index);
        return null;
    }

    private string? Rename(RenameOp rename)
    {
        var old = rename.Node.Value;
        var index = IndexOf(old);
        if (index < 0)
        {
            return $"there is no node '{old}'";
        }

        if (IndexOf(rename.To.Value) >= 0)
        {
            return $"node '{rename.To.Value}' already exists";
        }

        _nodes[index] = _nodes[index] with { Id = rename.To };
        for (var i = 0; i < _edges.Count; i++)
        {
            var edge = _edges[i];
            _edges[i] = edge with
            {
                From = edge.From.Value == old ? rename.To : edge.From,
                To = edge.To.Value == old ? rename.To : edge.To,
            };
        }

        // why: '{{old.field}}' follows in every config string, and so does the bare 'old.field' in a condition's 'when' text
        foreach (var node in _nodes)
        {
            ReferenceRename.Apply(node.Config, old, rename.To.Value);
            if (string.Equals(node.Type, "condition", StringComparison.Ordinal))
            {
                ReferenceRename.ApplyWhen(node.Config, old, rename.To.Value);
            }
        }

        return null;
    }

    private string? Meta(SetMetaOp meta)
    {
        if (meta.Name is { } name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "the name can't be empty";
            }

            _name = name;
        }

        if (meta.Description is { } description)
        {
            _description = description.Length == 0 ? null : description;
        }

        if (meta.Concurrency is { } concurrency)
        {
            _concurrency = concurrency.Length == 0 ? null : concurrency;
        }

        return null;
    }
}
