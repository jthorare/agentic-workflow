using System.Collections.Concurrent;
using Aw.Kernel;

namespace Aw.Sindri.Features;

/// <summary>
/// The last failed sign-in per connection name, in memory (connections PR 10a review): a sign-in that fails leaves no row, so without this an agent that started it
/// and the CLI that polls for it could only wait out the 5 minutes. GET /sindri/connections/{name} returns the failure's code and fixed title instead of SND-CONN-001
/// until the name signs in or is added again. It holds an <see cref="Error"/> already cut to its code and title, never a token. A restart forgets it.
/// </summary>
internal sealed class SignInOutcomes
{
    private readonly ConcurrentDictionary<string, Error> _failed = new(StringComparer.Ordinal);

    /// <summary>Remembers a failed sign-in.</summary>
    /// <param name="name">The connection name as text.</param>
    /// <param name="error">The failure, already safe to show.</param>
    internal void Failed(string name, Error error) => _failed[name] = error;

    /// <summary>Forgets the outcome (a new sign-in started, or one finished).</summary>
    /// <param name="name">The connection name as text.</param>
    internal void Clear(string name) => _ = _failed.TryRemove(name, out _);

    /// <summary>The last failure of <paramref name="name"/>, or null.</summary>
    /// <param name="name">The connection name as text.</param>
    /// <returns>The error, or null.</returns>
    internal Error? Last(string name) => _failed.GetValueOrDefault(name);
}
