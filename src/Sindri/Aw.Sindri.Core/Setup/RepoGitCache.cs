using System.Collections.Concurrent;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The git facts of the profile's repos (default branch, HEAD), kept for 30 seconds each and forgotten when a 'repo.*' event is written, so reading the setup list runs no git per request (setup-once spec §5.1 "Git facts").
/// A repo git can't read is remembered as unreadable for the same 30 seconds.
/// </summary>
/// <param name="inspector">Asks git.</param>
/// <param name="clock">Dates the entries.</param>
public sealed class RepoGitCache(IRepoInspector inspector, TimeProvider clock)
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, RepoGit? Facts)> _known = new(StringComparer.Ordinal);

    /// <summary>The facts of <paramref name="checkout"/>.</summary>
    /// <param name="checkout">The checkout's path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or null when git can't read the folder.</returns>
    public async ValueTask<RepoGit?> Of(string checkout, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_known.TryGetValue(checkout, out var kept) && now - kept.At < _lifetime)
        {
            return kept.Facts;
        }

        var read = await inspector.Facts(checkout, cancellationToken).ConfigureAwait(false);
        var facts = read.IsSuccess ? read.Value : null;
        _known[checkout] = (now, facts);
        return facts;
    }

    /// <summary>Forgets everything it knows: a repo was added or onboarded.</summary>
    public void Invalidate() => _known.Clear();
}
