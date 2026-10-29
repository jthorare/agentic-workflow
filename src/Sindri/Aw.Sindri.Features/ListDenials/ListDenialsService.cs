using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ListDenials;

/// <summary>
/// Lists the run areas' denial counters (sandbox spec §3.6), each entry with its fix and each launch with its item and repo; 'item' keeps one item's launches. '--since' keeps the entries last seen at or after it; a launch left with
/// nothing is dropped, and the overflow rows of a file stay only when the file itself was updated since.
/// </summary>
/// <param name="files">Where the counters are.</param>
/// <param name="owners">The item and repo of each launch.</param>
internal sealed class ListDenialsService(IDenialFiles files, ILaunchOwners owners) : IService<ListDenialsRequest, ListDenialsResponse>
{
    public async ValueTask<Result<ListDenialsResponse>> Handle(ListDenialsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var owned = await owners.Read(cancellationToken).ConfigureAwait(false);
        var all = (await files.List(request.Launch, cancellationToken).ConfigureAwait(false))
            .Select(f => Named(f, owned))
            .Where(f => request.Item is not { } item || f.Item == item)
            .ToList();
        if (request.Since is not { } since)
        {
            return new ListDenialsResponse(all);
        }

        var kept = all
            .Select(f => f with { Entries = [.. f.Entries.Where(e => e.Last >= since)], Overflow = f.UpdatedAt >= since ? f.Overflow : [] })
            .Where(static f => f.Entries.Count > 0 || f.Overflow.Count > 0);
        return new ListDenialsResponse([.. kept]);
    }

    // why: the counter is keyed by the launch alone, so the item and repo come from the ledger and the fix from the entry's kind and rule
    private static DenialFile Named(DenialFile file, IReadOnlyDictionary<LaunchId, LaunchOwner> owned)
    {
        var entries = file.Entries.Select(DenialFixes.Decide).ToList();
        return owned.TryGetValue(file.LaunchId, out var owner)
            ? file with { Entries = entries, Repo = owner.Repo, Item = owner.Item }
            : file with { Entries = entries };
    }
}
