using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearConnectionStore"/>: remembers what was saved and keeps no profile. A chosen key reads as the stored read key; a save can be made to fail.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal sealed class FakeLinearConnectionStore : ILinearConnectionStore
{
    private string? _readKey;
    private Error? _failing;

    /// <summary>Gets the read keys saved with their teams, in order.</summary>
    public IList<(string Key, IReadOnlyList<string> Teams)> ReadSaves { get; } = [];

    /// <summary>Gets the write keys saved, in order.</summary>
    public IList<string> WriteSaves { get; } = [];

    /// <summary>Gets how many times the write key was removed.</summary>
    public int Removals { get; private set; }

    /// <summary>Gets or sets the pointer a read save answers with, as the profile would hold it.</summary>
    public string ReadPointer { get; set; } = "keychain:sindri-index/linear";

    /// <summary>Gets or sets the pointer a write save answers with, as the profile would hold it.</summary>
    public string WritePointer { get; set; } = "keychain:sindri-index/linear-write";

    /// <summary>Makes <paramref name="key"/> the stored read key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionStore WithReadKey(string key)
    {
        _readKey = key;
        return this;
    }

    /// <summary>Fails every save with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionStore Failing(Error error)
    {
        _failing = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsReadKey(string key, CancellationToken cancellationToken) => ValueTask.FromResult(string.Equals(_readKey, key, StringComparison.Ordinal));

    /// <inheritdoc/>
    public ValueTask<Result<string>> SaveRead(string key, IReadOnlyList<string> teams, CancellationToken cancellationToken)
    {
        if (_failing is { } error)
        {
            return ValueTask.FromResult<Result<string>>(error);
        }

        ReadSaves.Add((key, teams));
        return ValueTask.FromResult<Result<string>>(ReadPointer);
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> SaveWrite(string key, CancellationToken cancellationToken)
    {
        if (_failing is { } error)
        {
            return ValueTask.FromResult<Result<string>>(error);
        }

        WriteSaves.Add(key);
        return ValueTask.FromResult<Result<string>>(WritePointer);
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> RemoveWrite(CancellationToken cancellationToken)
    {
        if (_failing is { } error)
        {
            return ValueTask.FromResult<Result<bool>>(error);
        }

        Removals++;
        return ValueTask.FromResult<Result<bool>>(true);
    }
}
