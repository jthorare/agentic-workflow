using Aw.Context.Core.Concepts;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The pin suggestion for the repo the caller may read, through the engine (which gates and scopes sessions). The request is checked first (SND-CONTEXT-179); nothing is written or queued.</summary>
internal sealed class GetContextPinSuggestService(ContextScopes scopes, IContextEngine engine) : IService<GetContextPinSuggestRequest, PinSuggestView>
{
    public async ValueTask<Result<PinSuggestView>> Handle(GetContextPinSuggestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = PinSuggestQueries.Of(request);
        if (query.IsError)
        {
            return query.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError
            ? scope.Error
            : await engine.PinSuggest(query.Value, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
