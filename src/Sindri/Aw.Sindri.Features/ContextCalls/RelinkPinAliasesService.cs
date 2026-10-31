using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a relink job (concept pins spec 3.4): asks the engine to link the stored records to the approved pins' aliases on the current map and keeps its counts as the job's result, which is where the
/// tick reads the pair (aliases hash, map version) the last relink was for. It runs no other link rule and calls no model; an engine error fails the job.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The running job: its progress takes the one line.</param>
internal sealed class RelinkPinAliasesService(ContextScopes scopes, IContextEngine engine, IJobContextAccessor jobs) : IService<RelinkPinAliasesRequest, RelinkPinAliasesJobResult>
{
    public async ValueTask<Result<RelinkPinAliasesJobResult>> Handle(RelinkPinAliasesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        jobs.Current?.Progress.Report("linking records to its words");
        var relinked = await engine.RelinkPinAliases(scope.Value, cancellationToken).ConfigureAwait(false);
        return relinked.IsError
            ? relinked.Error
            : new RelinkPinAliasesJobResult(relinked.Value.AliasesHash, relinked.Value.MapVersion, relinked.Value.Records, relinked.Value.Changed, relinked.Value.ElapsedMs, relinked.Value.Pins);
    }
}
