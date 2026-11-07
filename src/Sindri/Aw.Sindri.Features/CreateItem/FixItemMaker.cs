using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// <see cref="IFixItems"/> over the create-item handler itself, not its command chain: the fix command that calls it already holds the command gate (a second command inside it would wait the 5 s and fail),
/// is already audited and authorized, and checks the title and body it builds.
/// </summary>
/// <param name="service">The create-item handler.</param>
internal sealed class FixItemMaker(CreateItemService service) : IFixItems
{
    public async ValueTask<Result<ItemId>> Create(string title, RepoName? repo, string body, CancellationToken cancellationToken)
    {
        var request = new CreateItemRequest(title, repo, body);
        var valid = new CreateItemValidator().Validate(request);
        if (valid.IsError)
        {
            return valid.Error;
        }

        var made = await service.Handle(request, cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : made.Value.ItemId;
    }
}
