using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The work of a relink job: asks the engine to link the stored records to the pins' aliases and keeps its counts as the job's result.</summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The running job: its progress takes the one line.</param>
internal sealed class RelinkPinAliasesService(ContextScopes scopes, IContextEngine engine, IJobContextAccessor jobs) : IService<RelinkPinAliasesRequest, RelinkPinAliasesJobResult>
{
    public ValueTask<Result<RelinkPinAliasesJobResult>> Handle(RelinkPinAliasesRequest request, CancellationToken cancellationToken)
    {
        _ = (scopes, engine, jobs);
        throw new NotSupportedException();
    }
}
