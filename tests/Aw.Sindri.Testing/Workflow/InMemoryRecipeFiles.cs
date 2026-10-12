using Aw.Kernel;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IRecipeFiles"/>: recipe texts by path.</summary>
public sealed class InMemoryRecipeFiles : IRecipeFiles
{
    private readonly Dictionary<string, string> _byPath = new(StringComparer.Ordinal);

    /// <summary>Puts <paramref name="text"/> at <paramref name="path"/>.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="text">Its text.</param>
    /// <returns>This fake.</returns>
    public InMemoryRecipeFiles With(string path, string text)
    {
        _byPath[path] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Read(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_byPath.TryGetValue(path, out var text)
            ? Results.Success(text)
            : WorkflowErrors.RecipeUnreadable.With($"{path} doesn't exist (fix: check the path)"));
}
