using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IWatchedPulls"/> over the live instances (PR babysitter spec §6.2): every open instance whose current state is a wait on 'pr.changed'
/// (read from its approved recipe) and that holds a 'fact:pr'. An instance that can't be read this time, or holds no readable 'fact:pr', is left out
/// and found again on the next poll; only a failed listing is an error.
/// </summary>
/// <param name="instances">The open instances.</param>
/// <param name="reader">One instance's state and fact digests.</param>
/// <param name="recipes">The recipe an instance is pinned to.</param>
/// <param name="contents">The stored 'fact:pr'.</param>
internal sealed class InstanceWatchedPulls(IWorkflowInstances instances, IInstanceReader reader, IApprovedRecipes recipes, IFactContents contents) : IWatchedPulls
{
    private const string WaitKind = "pr.changed";
    private const string PrFact = "fact:pr";

    public async ValueTask<Result<IReadOnlyList<WatchedPull>>> List(CancellationToken cancellationToken)
    {
        var rows = await instances.List((string?)null, ended: false, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var found = new HashSet<WatchedPull>();
        var waits = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var row in rows.Value)
        {
            if (InstanceAddress.TryFrom(row.Address, out var address) && await Watched(address, waits, cancellationToken).ConfigureAwait(false) is { } pull)
            {
                _ = found.Add(pull);
            }
        }

        return Results.Success<IReadOnlyList<WatchedPull>>([.. found.OrderBy(static p => p.Item, StringComparer.Ordinal).ThenBy(static p => p.Link, StringComparer.Ordinal)]);
    }

    private async ValueTask<WatchedPull?> Watched(InstanceAddress address, Dictionary<string, bool> waits, CancellationToken cancellationToken)
    {
        var view = await reader.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError || view.Value.Outcome is not null || view.Value.ItemId is not { } item || view.Value.FactShas?.GetValueOrDefault(PrFact) is not { } sha
            || !await WaitsOnPrChanged(view.Value, waits, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var content = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        return content.IsError || Link(content.Value.Span) is not { } link ? null : new WatchedPull(link, item.Value);
    }

    private async ValueTask<bool> WaitsOnPrChanged(InstanceView view, Dictionary<string, bool> waits, CancellationToken cancellationToken)
    {
        var key = view.Recipe.Id.Value + "@" + view.Recipe.Hash.Value + "/" + view.State;
        if (waits.TryGetValue(key, out var known))
        {
            return known;
        }

        var recipe = await recipes.Find(view.Recipe.Id, view.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        var waiting = !recipe.IsError
            && recipe.Value.Recipe.States.TryGetValue(view.State, out var state)
            && string.Equals(state.WaitFor?.Event?.Value, WaitKind, StringComparison.Ordinal);
        waits[key] = waiting;
        return waiting;
    }

    /// <summary>The link a 'fact:pr' holds: a JSON string, or the bare text of the link.</summary>
    private static string? Link(ReadOnlySpan<byte> content)
    {
        var text = Encoding.UTF8.GetString(content).Trim();
        if (text.StartsWith('"'))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                text = document.RootElement.GetString() ?? string.Empty;
            }
            catch (JsonException)   // boundary: a fact that isn't a JSON string is read as it stands
            {
                return text.Length == 0 ? null : text;
            }
        }

        return text.Length == 0 ? null : text;
    }
}
