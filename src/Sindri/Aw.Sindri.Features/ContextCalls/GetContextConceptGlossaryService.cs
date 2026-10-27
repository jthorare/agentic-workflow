using Aw.Context.Core.Concepts;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The glossary of the concepts of the repo the caller may read, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextConceptGlossaryService(ContextScopes scopes, IContextEngine engine) : IService<GetContextConceptGlossaryRequest, ConceptGlossaryView>
{
    public async ValueTask<Result<ConceptGlossaryView>> Handle(GetContextConceptGlossaryRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError
            ? scope.Error
            : await engine.ConceptGlossary(new ConceptGlossaryQuery(read.Value, request.Repo), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
