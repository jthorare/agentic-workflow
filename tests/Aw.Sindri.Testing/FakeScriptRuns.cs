using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="IScriptRuns"/>: a run ends with the exit code its directory was given (<see cref="Exits"/>), 0 for a directory it was told nothing about; null for one given <see cref="CannotRun"/>.</summary>
public sealed class FakeScriptRuns : IScriptRuns
{
    private readonly Dictionary<string, int?> _exits = new(StringComparer.Ordinal);
    private readonly List<(string Directory, string Script)> _calls = [];

    /// <summary>Every run so far, in order.</summary>
    public IReadOnlyList<(string Directory, string Script)> Calls => _calls;

    /// <summary>Makes a script run in <paramref name="directory"/> exit with <paramref name="code"/>.</summary>
    /// <param name="directory">The checkout.</param>
    /// <param name="code">The exit code.</param>
    /// <returns>The same instance.</returns>
    public FakeScriptRuns Exits(string directory, int code)
    {
        _exits[directory] = code;
        return this;
    }

    /// <summary>Makes a script in <paramref name="directory"/> unable to run.</summary>
    /// <param name="directory">The checkout.</param>
    /// <returns>The same instance.</returns>
    public FakeScriptRuns CannotRun(string directory)
    {
        _exits[directory] = null;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<int?> Run(string directory, string script, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _calls.Add((directory, script));
        return ValueTask.FromResult(_exits.TryGetValue(directory, out var code) ? code : 0);
    }
}
