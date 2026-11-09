using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Applies a batch of <see cref="GraphOp"/>s to a workflow graph.</summary>
public static partial class GraphOps
{
    private const string DefaultOutput = "out";

    /// <summary>
    /// Applies <paramref name="ops"/> in order to a copy of <paramref name="spec"/>: all of them or none. A <paramref name="baseRevision"/> that is not
    /// <paramref name="currentRevision"/> is SND-GRAPH-007 naming the current revision; an op that can't apply is SND-GRAPH-012 naming the op's number;
    /// a result the validator blocks (draft mode, so unwired nodes pass) is SND-GRAPH-002 listing the findings. <paramref name="spec"/> is never changed.
    /// </summary>
    /// <param name="spec">The graph at <paramref name="currentRevision"/>.</param>
    /// <param name="baseRevision">The revision the caller read before it made these ops.</param>
    /// <param name="currentRevision">The latest revision now.</param>
    /// <param name="ops">The edits, applied in order.</param>
    /// <param name="validator">Checks the edited graph.</param>
    /// <returns>The edited graph, or why nothing changed.</returns>
    public static Result<GraphSpec> Apply(GraphSpec spec, int baseRevision, int currentRevision, IReadOnlyList<GraphOp> ops, GraphValidator validator)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(validator);
        if (baseRevision != currentRevision)
        {
            var current = currentRevision.ToString(CultureInfo.InvariantCulture);
            return SindriErrors.GraphStaleRevision
                .With($"This workflow is at revision {current}, and these ops were made on revision {baseRevision.ToString(CultureInfo.InvariantCulture)}. Read it again (current revision {current}) and retry.")
                .WithExtension("currentRevision", JsonDocument.Parse(current).RootElement.Clone());
        }

        if (ops.Count == 0)
        {
            return SindriErrors.GraphOpInvalid.With("send at least one op");
        }

        var working = new Working(spec);
        for (var i = 0; i < ops.Count; i++)
        {
            if (working.Apply(ops[i]) is { } problem)
            {
                return SindriErrors.GraphOpInvalid.With($"op {(i + 1).ToString(CultureInfo.InvariantCulture)} ({Kind(ops[i])}): {problem}");
            }
        }

        var edited = working.ToSpec();
        var validation = validator.Validate(edited, GraphMode.Draft);
        return validation.Passed
            ? edited
            : SindriErrors.GraphFindings.With(string.Join("; ", validation.Blocking.Select(static f => f.ToString())));
    }

    private static string Kind(GraphOp op) => op switch
    {
        AddNodeOp => "add_node",
        SetConfigOp => "set_config",
        RemoveNodeOp => "remove_node",
        ConnectOp => "connect",
        DisconnectOp => "disconnect",
        RenameOp => "rename",
        SetMetaOp => "set_meta",
        _ => "unknown",
    };

    [GeneratedRegex("(\\{\\{\\s*)(?<id>[a-z][a-z0-9_]*)(?=[.\\s}])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference();

    private sealed class Working(GraphSpec spec)
    {
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
            _ => "that is not a known op",
        };

        private int IndexOf(string id) => _nodes.FindIndex(n => string.Equals(n.Id.Value, id, StringComparison.Ordinal));

        private string? Add(AddNodeOp add)
        {
            if (IndexOf(add.Id.Value) >= 0)
            {
                return $"node '{add.Id.Value}' already exists";
            }

            _nodes.Add(new GraphNode(add.Id, add.Type, add.Title, add.Config is null ? new JsonObject() : (JsonObject)add.Config.DeepClone(), add.OnFailure));
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
                var step = Step(holder, keys[i], create: true);
                if (step.Problem is not null)
                {
                    return step.Problem;
                }

                holder = step.Next!;
            }

            return Put(holder, keys[^1], set.Value?.DeepClone());
        }

        // why: a missing object on the path is created; anything else that isn't an object or a list item is a path error, not a silent overwrite.
        private static (JsonNode? Next, string? Problem) Step(JsonNode holder, string key, bool create)
        {
            switch (holder)
            {
                case JsonObject obj:
                    if (obj[key] is { } existing)
                    {
                        return existing is JsonObject or JsonArray ? (existing, null) : (null, $"'{key}' holds a value, not an object, so the path can't go through it");
                    }

                    if (!create)
                    {
                        return (null, $"'{key}' is not there");
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

            if (_edges.Any(e => Same(e, connect.From, connect.Output, connect.To)))
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

            // why: only '{{old.field}}' references follow; a bare name inside a condition's 'when' text is left for the validator to flag.
            foreach (var node in _nodes)
            {
                Rewrite(node.Config, old, rename.To.Value);
            }

            return null;
        }

        private static void Rewrite(JsonNode? node, string old, string next)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(static p => p.Key).ToList())
                    {
                        if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text) && text.Contains("{{", StringComparison.Ordinal))
                        {
                            obj[key] = Reference().Replace(text, m => string.Equals(m.Groups["id"].Value, old, StringComparison.Ordinal) ? m.Groups[1].Value + next : m.Value);
                        }
                        else
                        {
                            Rewrite(obj[key], old, next);
                        }
                    }

                    break;
                case JsonArray list:
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is JsonValue value && value.TryGetValue<string>(out var text) && text.Contains("{{", StringComparison.Ordinal))
                        {
                            list[i] = Reference().Replace(text, m => string.Equals(m.Groups["id"].Value, old, StringComparison.Ordinal) ? m.Groups[1].Value + next : m.Value);
                        }
                        else
                        {
                            Rewrite(list[i], old, next);
                        }
                    }

                    break;
                default:
                    break;
            }
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
}
