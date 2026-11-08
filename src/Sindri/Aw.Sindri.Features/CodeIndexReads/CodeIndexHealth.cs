using Aw.CodeIndex.Core.Facade;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// Reads every approved or live-only repo's index the way the code-index status read does and maps each to its <see cref="CodeIndexProblems"/> row. The scope is a service's: all the approved profile's repos and the live profile's others (owner q102, CIX-L59),
/// not one caller's. Nothing approved, or an index, head or job read that fails, adds no row: status never fails over the index.
/// </summary>
internal sealed class CodeIndexHealth(ICodeIndex index, RepoHealths healths, IProfileSource live) : ICodeIndexHealth
{
    public async ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (approved is null)
        {
            return [];
        }

        var file = await live.Load(cancellationToken).ConfigureAwait(false);
        var scope = CodeIndexScopes.For(approved.Profile, file.IsError ? null : file.Value.Profile, caller: null);
        if (scope.IsError)
        {
            return [];
        }

        var answer = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return [];
        }

        var repos = answer.Value.Repos;
        var overlaid = await healths.Of(scope.Value, repos, cancellationToken).ConfigureAwait(false);
        return [.. repos.Zip(overlaid, CodeIndexProblems.For).OfType<HealthProblem>()];
    }
}
