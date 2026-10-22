using System.Runtime.CompilerServices;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.AddRepo;

/// <summary>
/// The folders the admission resolved, kept for the job key and for the job that does the work: the start checks the folder inside the gate (so a refusal is a synchronous error), the key is the hash of the resolved
/// path, and the job runs afterwards outside the gate. Keyed by the request itself, so nothing but that start's job can take it.
/// </summary>
internal sealed class ResolvedRepos
{
    private readonly ConditionalWeakTable<AddRepoRequest, ResolvedRepo> _held = [];

    public void Keep(AddRepoRequest request, ResolvedRepo repo) => _held.AddOrUpdate(request, repo);

    public void Drop(AddRepoRequest request) => _ = _held.Remove(request);

    /// <summary>The job key of a start the admission accepted.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key.</returns>
    public JobKey KeyOf(AddRepoRequest request) =>
        _held.TryGetValue(request, out var repo) ? RepoFolders.KeyOf(repo.Inspected.Checkout) : throw new InvalidOperationException("the AddRepo start was not admitted");

    public bool Take(AddRepoRequest request, out ResolvedRepo? repo)
    {
        if (!_held.TryGetValue(request, out repo))
        {
            return false;
        }

        _ = _held.Remove(request);
        return true;
    }
}
