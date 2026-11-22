using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.GetConcepts;

/// <summary>The concept answer store of a host that wired none: it knows no basis and keeps nothing, so every concepts and glossary read builds its answer. Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnkeptConceptAnswerStore : IConceptAnswerStore
{
    public ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ConceptAnswerBasis?>(value: null));

    public ValueTask<Result<ConceptsView?>> Concepts(string key, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ConceptsView?>(value: null));

    public ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ConceptGlossaryView?>(value: null));

    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptsView view, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));

    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));
}
