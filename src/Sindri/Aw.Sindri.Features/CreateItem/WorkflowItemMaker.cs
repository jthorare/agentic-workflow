using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// <see cref="ICreateItemPort"/> over the create-item handler itself, not its command chain (as <see cref="FixItemMaker"/> does): a 'create_item' node makes its item the way 'sindri item add'
/// does, as 'service:workflow'. The node's run is already audited, and a workflow's owner approved it.
/// Scoped, like the handler: <see cref="ScopedCreateItemPort"/> is the engine's singleton port and runs this one in a scope per item.
/// </summary>
/// <param name="service">The create-item handler.</param>
/// <param name="callers">Enters 'service:workflow' for the handler.</param>
/// <param name="runs">Links the run to the item it made.</param>
internal sealed class WorkflowItemMaker(CreateItemService service, ICallerContextAccessor callers, IWorkflowRunStore runs) : ICreateItemPort
{
    public async ValueTask<Result<string>> Create(ItemToCreate item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var parsed = ToRequest(item);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        var run = item.Run;
        var request = parsed.Value with { RunRecord = RecordKey.TryFrom("sindri:run/" + run.Value, out var runRecord) ? runRecord : null };
        using var entered = callers.EnterService("workflow");
        var made = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var linked = await runs.SetItem(run, made.Value.ItemId, cancellationToken).ConfigureAwait(false);
        return linked.IsError ? linked.Error : made.Value.ItemId.Value;
    }

    private static Result<CreateItemRequest> ToRequest(ItemToCreate item)
    {
        var (_, title, repo, body, linearIssue) = item;
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
        return valid.IsError ? valid.Error : request;
    }
}
