using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IWatchedPulls"/> from the workflow instances: each open instance whose current state's wait names 'pr.changed' in its approved recipe, with its 'fact:pr' read from the fact store.
/// An instance that ended, or whose fact can't be read, between the listing and the read is left out of this poll.
/// </summary>
/// <param name="instances">The open instances (a visibility query).</param>
/// <param name="reader">An instance's view: its item and the sha of each fact.</param>
/// <param name="recipes">The approved recipes.</param>
/// <param name="contents">The fact store's content.</param>
internal sealed class TemporalWatchedPulls(IWorkflowInstances instances, IInstanceReader reader, IApprovedRecipes recipes, IFactContents contents) : IWatchedPulls
{
    private const string Event = "pr.changed";
    private const string PrFact = "fact:pr";

    public async ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken)
    {
        var open = await instances.List((string?)null, ended: false, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var waits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var found = new List<WatchedPull>();
        foreach (var row in open.Value)
        {
            if (row.Recipe is not { Length: > 0 } recipe || row.State is not { Length: > 0 } state)
            {
                continue;
            }

            if (!waits.TryGetValue(recipe, out var states))
            {
                waits[recipe] = states = await Waiting(recipe, cancellationToken).ConfigureAwait(false);
            }

            if (states.Contains(state) && await Watched(row.Address, cancellationToken).ConfigureAwait(false) is { } pull)
            {
                found.Add(pull);
            }
        }

        return found;
    }

    /// <summary>The pull request's link in a 'fact:pr' fact: a JSON string, or an object with 'url' or 'link'.</summary>
    /// <param name="json">The fact's JSON.</param>
    /// <returns>The link, or null when the fact holds none.</returns>
    internal static string? LinkOf(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var text = root.ValueKind == JsonValueKind.Object ? Named(root, "url") ?? Named(root, "link") : root.ValueKind == JsonValueKind.String ? root.GetString() : null;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Named(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The states of the latest approved recipe whose wait is for 'pr.changed'; none for a recipe that isn't approved.</summary>
    private async ValueTask<HashSet<string>> Waiting(string recipe, CancellationToken cancellationToken)
    {
        var latest = await recipes.Latest(RecipeId.From(recipe), cancellationToken).ConfigureAwait(false);
        return latest.IsError
            ? []
            : [.. latest.Value.Recipe.States.Where(static s => s.Value.WaitFor?.Event is { } waited && string.Equals(waited.Value, Event, StringComparison.Ordinal)).Select(static s => s.Key)];
    }

    private async ValueTask<WatchedPull?> Watched(string address, CancellationToken cancellationToken)
    {
        var view = await reader.Get(InstanceAddress.From(address), cancellationToken).ConfigureAwait(false);
        if (view.IsError || view.Value.ItemId is not { } item || view.Value.FactShas is not { } shas || !shas.TryGetValue(PrFact, out var sha))
        {
            return null;
        }

        var content = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        return content.IsError || LinkOf(content.Value.Span) is not { } link ? null : new WatchedPull(item.Value, link);
    }
}
