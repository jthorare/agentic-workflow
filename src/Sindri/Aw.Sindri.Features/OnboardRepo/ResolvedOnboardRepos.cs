using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.OnboardRepo;

/// <summary>The repos the OnboardRepo admission resolved, kept for the job key and the job's work, keyed by the request itself so nothing but that start's job can take it.</summary>
internal sealed class ResolvedOnboardRepos
{
    private readonly ConditionalWeakTable<OnboardRepoRequest, Box> _held = [];

    public void Keep(OnboardRepoRequest request, RepoName repo) => _held.AddOrUpdate(request, new Box(repo));

    public void Drop(OnboardRepoRequest request) => _ = _held.Remove(request);

    /// <summary>The job key of a start the admission accepted: 'onboard-' and the first 16 hex of the sha256 of the repo's name.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key.</returns>
    public JobKey KeyOf(OnboardRepoRequest request) =>
        _held.TryGetValue(request, out var box)
            ? JobKey.From("onboard-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(box.Repo.Value)))[..16])
            : throw new InvalidOperationException("the OnboardRepo start was not admitted");

    public RepoName? Take(OnboardRepoRequest request)
    {
        if (!_held.TryGetValue(request, out var box))
        {
            return null;
        }

        _ = _held.Remove(request);
        return box.Repo;
    }

    private sealed record Box(RepoName Repo);
}
