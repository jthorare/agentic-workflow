using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Kernel;

namespace Aw.Context.Testing.Glossary;

/// <summary>An <see cref="ISymbolVectors"/> over a fixed table of file vectors; <see cref="SymbolVectorsContract{TSource}"/> holds every source to its behavior.</summary>
public sealed class FakeSymbolVectors : ISymbolVectors
{
    private readonly Dictionary<string, List<SymbolVector>> _byPath = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails with it (the repo has no index, say).</summary>
    public Error? Failure { get; set; }

    /// <summary>The number of paths of each call, in order.</summary>
    public IList<int> Asked { get; } = [];

    /// <summary>Gives a file one more embedded symbol.</summary>
    /// <param name="path">The file's repo-relative path.</param>
    /// <param name="name">The symbol's name.</param>
    /// <param name="vector">Its embedding.</param>
    /// <returns>This fake.</returns>
    public FakeSymbolVectors With(string path, string name, params float[] vector)
    {
        if (!_byPath.TryGetValue(path, out var symbols))
        {
            _byPath[path] = symbols = [];
        }

        symbols.Add(new SymbolVector(name, vector));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<SymbolVectorsRead>> Read(ContextRepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Asked.Add(paths.Count);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<SymbolVectorsRead>>(failure);
        }

        var found = paths.Where(_byPath.ContainsKey).Distinct(StringComparer.Ordinal).SelectMany(p => _byPath[p].Select(s => new PathVector(p, s))).ToList();
        var missing = paths.Where(p => !_byPath.ContainsKey(p)).Distinct(StringComparer.Ordinal).ToList();
        return ValueTask.FromResult(Results.Success(new SymbolVectorsRead(found, missing)));
    }
}
