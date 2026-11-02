using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Kernel;

namespace Aw.Context.Testing.Audience;

/// <summary>An <see cref="IAudiencePolicy"/> over a fixed table of repo to audiences; it passes <see cref="AudiencePolicyContract{TPolicy}"/>.</summary>
/// <param name="byRepo">What each repo's sessions may read.</param>
public sealed class FakeAudiencePolicy(IReadOnlyDictionary<ContextRepoName, IReadOnlyList<AudienceKey>> byRepo) : IAudiencePolicy
{
    /// <inheritdoc />
    public ValueTask<Result<AudienceScope>> For(ContextRepoName repo, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<AudienceScope>>(byRepo.TryGetValue(repo, out var allowed) ? new AudienceScope(allowed.ToHashSet()) : AudienceScope.None);
}
