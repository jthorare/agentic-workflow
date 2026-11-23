using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConceptGlossary;

/// <summary>The glossary of the map the caller may read: the answer kept for this map version and caller, or built by <see cref="ConceptGlossaryBuilder"/> from the terms stored on the concepts and kept (reads never call a model or the code index).</summary>
internal sealed class GetConceptGlossaryService(ConceptAnswers answers) : IService<GetConceptGlossaryRequest, ConceptGlossaryView>
{
    public ValueTask<Result<ConceptGlossaryView>> Handle(GetConceptGlossaryRequest request, CancellationToken cancellationToken) =>
        answers.Glossary(request.Scope, request.Query, cancellationToken);
}
