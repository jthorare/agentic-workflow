using System.Runtime.CompilerServices;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The repos the RegisterMcp admission resolved, kept for the job key and the job's work, keyed by the request itself so nothing but that start's job can take it.</summary>
internal sealed class ResolvedMcpRepos
{
    private readonly ConditionalWeakTable<RegisterMcpJobRequest, Box> _held = [];

    public void Keep(RegisterMcpJobRequest request, RepoName repo) => _held.AddOrUpdate(request, new Box(repo));

    public void Drop(RegisterMcpJobRequest request) => _ = _held.Remove(request);

    /// <summary>The job key of a start the admission accepted: 'mcp-' and the first 16 hex of the sha256 of the repo's name.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The key.</returns>
    public JobKey KeyOf(RegisterMcpJobRequest request) =>
        _held.TryGetValue(request, out var box) ? McpJobKeys.For(box.Repo) : throw new InvalidOperationException("the RegisterMcp start was not admitted");

    public RepoName? Take(RegisterMcpJobRequest request)
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
