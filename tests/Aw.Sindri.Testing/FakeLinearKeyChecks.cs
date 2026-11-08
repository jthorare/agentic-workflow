using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearKeyChecks"/>: the teams every key reads and the scope every write key has, or a failure. Records the keys it was asked about.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal sealed class FakeLinearKeyChecks : ILinearKeyChecks
{
    private readonly List<string> _asked = [];
    private IReadOnlyList<LinearConnectionTeamView> _teams = [];
    private LinearKeyScope _scope = LinearKeyScope.Writable;
    private Error? _failing;

    /// <summary>Gets the keys asked about, in order.</summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>Answers a teams read with <paramref name="teams"/>.</summary>
    /// <param name="teams">The teams.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearKeyChecks Reading(params LinearConnectionTeamView[] teams)
    {
        _teams = teams;
        return this;
    }

    /// <summary>Answers a write-scope check with <paramref name="scope"/>.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearKeyChecks WithScope(LinearKeyScope scope)
    {
        _scope = scope;
        return this;
    }

    /// <summary>Fails every check with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearKeyChecks Failing(Error error)
    {
        _failing = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<LinearConnectionTeamView>>> Teams(string key, CancellationToken cancellationToken)
    {
        _asked.Add(key);
        return ValueTask.FromResult(_failing is { } error ? Results.Failure<IReadOnlyList<LinearConnectionTeamView>>(error) : Results.Success(_teams));
    }

    /// <inheritdoc/>
    public ValueTask<Result<LinearKeyScope>> WriteScope(string key, CancellationToken cancellationToken)
    {
        _asked.Add(key);
        return ValueTask.FromResult(_failing is { } error ? Results.Failure<LinearKeyScope>(error) : Results.Success(_scope));
    }
}
