using System.Collections.Concurrent;
using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// The last failed sign-in per connection name, in memory (connections PR 10a review): a sign-in that fails leaves no row, so without this an agent that started it
/// and the CLI that polls for it could only wait out the 5 minutes. GET /sindri/connections/{name} returns the failure's code and fixed title instead of SND-CONN-001
/// until the name signs in or is added again. It holds an <see cref="Error"/> already cut to its code and title, never a token. A restart forgets it. Each sign-in is a <see cref="SignInTry"/>: the end of a superseded try is ignored, so an older try that ends after a newer one began cannot overwrite what the newer one reads (issue #2420).
/// </summary>
public sealed class SignInOutcomes
{
    private readonly ConcurrentDictionary<string, SignInFailure> _failed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _latest = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>Starts a new try of <paramref name="name"/>: it becomes the latest try and the name's failure is forgotten.</summary>
    /// <param name="name">The connection name as text.</param>
    /// <returns>The new try; only it may record or clear the name's outcome until a newer one begins.</returns>
    public SignInTry Begin(string name)
    {
        lock (_gate)
        {
            var number = _latest.GetValueOrDefault(name) + 1;
            _latest[name] = number;
            _ = _failed.TryRemove(name, out _);
            return new SignInTry(name, number);
        }
    }

    /// <summary>Remembers a failed sign-in, unless a newer try of the same name has begun.</summary>
    /// <param name="attempt">The try that failed.</param>
    /// <param name="error">The failure, already safe to show.</param>
    /// <param name="at">When it failed.</param>
    public void Failed(SignInTry attempt, Error error, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        lock (_gate)
        {
            if (IsLatest(attempt))
            {
                _failed[attempt.Name] = new SignInFailure(error, at);
            }
        }
    }

    /// <summary>Forgets the outcome (the try finished), unless a newer try of the same name has begun.</summary>
    /// <param name="attempt">The try that finished.</param>
    public void Clear(SignInTry attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        lock (_gate)
        {
            if (IsLatest(attempt))
            {
                _ = _failed.TryRemove(attempt.Name, out _);
            }
        }
    }

    private bool IsLatest(SignInTry attempt) => _latest.GetValueOrDefault(attempt.Name) == attempt.Number;

    /// <summary>The last failure of <paramref name="name"/>, or null.</summary>
    /// <param name="name">The connection name as text.</param>
    /// <returns>The error, or null.</returns>
    public Error? Last(string name) => _failed.GetValueOrDefault(name)?.Error;

    /// <summary>The last failure of <paramref name="name"/> with its time, or null.</summary>
    /// <param name="name">The connection name as text.</param>
    /// <returns>The failure, or null.</returns>
    public SignInFailure? LastFailure(string name) => _failed.GetValueOrDefault(name);
}
