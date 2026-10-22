using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>An <see cref="IPackDirectories"/> in memory: the directories a test gave, an error for any other. Held to <see cref="PackDirectoriesContract"/>.</summary>
public sealed class InMemoryPackDirectories : IPackDirectories
{
    private readonly Dictionary<string, PackDirectory> _directories = new(StringComparer.Ordinal);

    /// <summary>Holds a directory.</summary>
    /// <param name="directory">The directory's path.</param>
    /// <param name="files">Its files by path inside the pack.</param>
    /// <param name="outside">The entries that point outside it.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPackDirectories With(string directory, IReadOnlyDictionary<string, string> files, params string[] outside)
    {
        _directories[directory] = new PackDirectory(files, outside);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<PackDirectory>> Read(string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_directories.TryGetValue(directory, out var found)
            ? Results.Success(found)
            : SindriErrors.InvalidInput.With($"{directory} is not a pack directory the fake knows"));
    }
}
