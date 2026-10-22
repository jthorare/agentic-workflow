using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>Scratch checkouts held in memory: path to commit. Held to <see cref="ScratchCheckoutsContract"/>.</summary>
public sealed class FakeScratchCheckouts : IScratchCheckouts
{
    private readonly Dictionary<string, string> _checkouts = new(StringComparer.Ordinal);
    private readonly List<string> _created = [];
    private readonly List<string> _removed = [];

    /// <summary>Gets the checkouts that exist now: path to commit.</summary>
    public IReadOnlyDictionary<string, string> Checkouts => _checkouts;

    /// <summary>Gets the paths <see cref="Create"/> made a checkout at, in order.</summary>
    public IReadOnlyList<string> Created => _created;

    /// <summary>Gets the paths <see cref="Remove"/> removed a checkout from, in order.</summary>
    public IReadOnlyList<string> Removed => _removed;

    /// <summary>Gets or sets an error every <see cref="Create"/> returns instead of making a checkout.</summary>
    public Error? FailWith { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Create(string repoPath, string path, string headSha, CancellationToken cancellationToken)
    {
        if (path.Length == 0 || path[0] != '/')
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.RequestInvalid.With("the scratch checkout's path isn't absolute"));
        }

        if (headSha.Length is < 7 or > 64 || !headSha.All(static c => char.IsAsciiHexDigit(c)))
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.RequestInvalid.With("the scratch checkout's commit isn't hex"));
        }

        if (FailWith is { } failure)
        {
            return ValueTask.FromResult<Result<string>>(failure);
        }

        if (!_checkouts.TryGetValue(path, out var existing) || existing != headSha)
        {
            _checkouts[path] = headSha;
            _created.Add(path);
        }

        return ValueTask.FromResult<Result<string>>(path);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        if (_checkouts.Remove(path))
        {
            _removed.Add(path);
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
