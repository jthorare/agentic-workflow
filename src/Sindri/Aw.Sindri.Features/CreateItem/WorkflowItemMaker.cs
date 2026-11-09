using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// <see cref="ICreateItemPort"/> over the create-item handler itself, not its command chain (as <see cref="FixItemMaker"/> does): a 'create_item' node makes its item the way 'sindri item add'
/// does, as 'service:workflow'. The node's run is already audited, and a workflow's owner approved it.
/// </summary>
/// <param name="scopes">Makes a scope per item; the handler is scoped and the engine's blocks are not.</param>
internal sealed class WorkflowItemMaker(IServiceScopeFactory scopes) : ICreateItemPort
{
    public async ValueTask<Result<string>> Create(ItemToCreate item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var (run, title, repo, body, linearIssue) = item;
        RepoName? repoName = null;
        if (repo is not null)
        {
            if (!RepoName.TryFrom(repo, out var named))
            {
                return SindriErrors.InvalidInput.With($"'{repo}' is not a repo name (fix: use a repo of the profile, such as 'web')");
            }

            repoName = named;
        }

        RecordKey? link = null;
        if (linearIssue is not null)
        {
            if (!RecordKey.TryFrom(linearIssue, out var key))
            {
                return SindriErrors.InvalidInput.With($"'{linearIssue}' is not a record key like linear:issue/<id>");
            }

            link = key;
        }

        var request = new CreateItemRequest(title, repoName, body, link);
        var valid = new CreateItemValidator().Validate(request);
        if (valid.IsError)
        {
            return valid.Error;
        }

        using var scope = scopes.CreateScope();   // why: the handler is scoped; the run store is a singleton
        using var entered = scope.ServiceProvider.GetRequiredService<ICallerContextAccessor>().EnterService("workflow");
        var made = await scope.ServiceProvider.GetRequiredService<CreateItemService>().Handle(request, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var linked = await scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>().SetItem(run, made.Value.ItemId, cancellationToken).ConfigureAwait(false);
        return linked.IsError ? linked.Error : made.Value.ItemId.Value;
    }
}
