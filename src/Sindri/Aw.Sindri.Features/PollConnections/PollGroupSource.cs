using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Collects the connection_event triggers of every active workflow and groups them by (connection, event kind), so one provider call serves all of a group's triggers.</summary>
/// <param name="workflows">The workflows and their revisions.</param>
internal sealed class PollGroupSource(IWorkflowStore workflows)
{
    internal async ValueTask<Result<IReadOnlyList<PollGroup>>> Collect(CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var found = new Dictionary<(ConnectionName, string), (EventKind Kind, List<PollTrigger> Triggers)>();
        foreach (var record in listed.Value.Where(static r => r.ActiveRevision is not null))
        {
            var revision = await workflows.GetRevision(record.Id, record.ActiveRevision, cancellationToken).ConfigureAwait(false);
            if (revision.IsError)
            {
                return revision.Error;   // why: a store error must not look like "no triggers"; the next run reads again
            }

            var node = revision.Value.Spec.Nodes.FirstOrDefault(static n => string.Equals(n.Type, TriggerMatch.Kind, StringComparison.Ordinal));
            if (node is null || Watched(node.Config) is not { } watched)
            {
                continue;
            }

            var (connection, kind) = watched;

            var trigger = new PollTrigger(record.Id, record.Slug, revision.Value.Revision, Concurrency(revision.Value.Spec.Concurrency), Text(node.Config, "filter"));
            if (!found.TryGetValue((connection, kind.Key), out var group))
            {
                group = (kind, []);
                found[(connection, kind.Key)] = group;
            }

            group.Triggers.Add(trigger);
        }

        return Results.Success<IReadOnlyList<PollGroup>>([.. found.OrderBy(static p => p.Key.Item1.ToString(), StringComparer.Ordinal).ThenBy(static p => p.Key.Item2, StringComparer.Ordinal)
            .Select(static p => new PollGroup(p.Key.Item1, p.Value.Kind, p.Value.Triggers))]);
    }

    // The (connection, kind) a trigger names, or null when its config doesn't name a connection and a catalog event (the validator refuses that when the workflow is saved).
    private static (ConnectionName Connection, EventKind Kind)? Watched(JsonObject config)
    {
        if (Text(config, "connection") is not { } name || Text(config, "event") is not { } key || PollCatalog.Find(key) is not { } kind)
        {
            return null;
        }

        var parsed = ConnectionName.Parse(name);
        return parsed.IsError ? null : (parsed.Value, kind);
    }

    private static string? Text(JsonObject config, string name) => config[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };
}
