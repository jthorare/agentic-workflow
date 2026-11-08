using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// How the router stub routes one started instance (engine P7 plan D8, workflow engine spec §12): a 'workflow.item' root is
/// 'startable' unless the approved profile's 'workflow.route.startable' says 'none', and its Step spec is the 'spec' of the
/// stored 'fact:item', stored again as 'fact:stepSpec'. Any other instance is not the stub's.
/// </summary>
/// <param name="instances">Reads an instance's recipe and fact digests.</param>
/// <param name="store">Reads 'fact:item' and stores 'fact:stepSpec'.</param>
/// <param name="profile">The approved profile ('workflow.route').</param>
internal sealed class ItemRouter(IInstanceReader instances, HomeFactStore store, IApprovedProfile profile)
{
    private static readonly Result<RoutedItem?> _none = Results.Success<RoutedItem?>(value: null);
    private const string ItemRecipe = "workflow.item";
    private static readonly FactKey _item = FactKey.From("fact:item");
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    /// <summary>The 'item.routed' payload for the instance at <paramref name="address"/>.</summary>
    /// <param name="address">The started instance.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The payload; null when the instance isn't a 'workflow.item' root the stub routes (or has gone); an error when a read failed and the stub should try again.</returns>
    public async ValueTask<Result<RoutedItem?>> Route(string address, CancellationToken cancellationToken)
    {
        if (!IsItemRoot(address) || !InstanceAddress.TryFrom(address, out var instance))
        {
            return _none;
        }

        var view = await instances.Get(instance, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            if (string.Equals(view.Error.Code, WorkflowErrors.InstanceNotFound.Code, StringComparison.Ordinal))
            {
                return _none;   // it has gone: nothing to route
            }

            return view.Error;
        }

        if (!string.Equals(view.Value.Recipe.Id.Value, ItemRecipe, StringComparison.Ordinal))
        {
            return _none;
        }

        return await Decide(view.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<RoutedItem?>> Decide(InstanceView view, CancellationToken cancellationToken)
    {
        var spec = await SpecOf(view, cancellationToken).ConfigureAwait(false);
        if (spec.IsError)
        {
            return spec.Error;
        }

        var stored = await store.Put(Encoding.UTF8.GetBytes(spec.Value), cancellationToken).ConfigureAwait(false);
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        if (latest.IsError)
        {
            return latest.Error;
        }

        var startable = latest.Value is not { } approved || RouteSettings.StartableOf(approved.Profile);
        return Results.Success<RoutedItem?>(new RoutedItem(startable ? "startable" : "notStartable", new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["fact:stepSpec"] = stored.Value }));
    }

    /// <summary>A root address is '{item}/{recipe name}/{attempt}'; a child has more parts and is never routed.</summary>
    private static bool IsItemRoot(string address)
    {
        var parts = address.Split('/');
        return parts.Length == 3 && string.Equals(parts[1], "item", StringComparison.Ordinal);
    }

    private async ValueTask<Result<string>> SpecOf(InstanceView view, CancellationToken cancellationToken)
    {
        if (view.FactShas?.GetValueOrDefault(_item.Value) is not { } sha)
        {
            return string.Empty;
        }

        var read = await store.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var item = AwJson.Deserialize<ItemFact>(read.Value.ToArray(), _json);
        return item.IsError ? string.Empty : item.Value.Spec ?? string.Empty;   // an item fact that doesn't read routes with an empty spec; the Step spec is the item's own words
    }
}
