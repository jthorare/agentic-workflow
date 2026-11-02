using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The source a host without a code index gets: no file has a vector, so a build derives its concepts without a glossary and says nothing about it.</summary>
internal sealed class DisabledSymbolVectors : ISymbolVectors
{
    public ValueTask<Result<SymbolVectorsRead>> Read(ContextRepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new SymbolVectorsRead([], paths)));
}
