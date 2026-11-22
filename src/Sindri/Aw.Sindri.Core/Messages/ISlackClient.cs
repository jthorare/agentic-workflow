using Aw.Kernel;

namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The Slack Web API port Messages reads through (messages spec §5.3, §8.1): one HTTP client that sends the token only to slack.com hosts (or the
/// fixture's loopback base, <see cref="SlackBaseUrlRule"/>), follows no redirect, and draws every call from the one <see cref="SlackRateBudget"/>, so a
/// 429 holds only that method (SND-MSG-005). Each call takes the token, so a pasted token can be tried before it is stored. The read methods the sync
/// needs arrive with it (M1e).
/// </summary>
public interface ISlackClient
{
    /// <summary>Calls auth.test: which workspace and user the token belongs to.</summary>
    /// <param name="token">The User OAuth Token. Never logged or put in an error.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The workspace and user, or SND-MSG-002 (revoked or invalid), 003 (a missing scope), 005 (rate limited), 019 (the workspace blocks the
    /// app), 033 (Slack unreachable) or SND-CONN-012 (an answer that is not Slack's shape).</returns>
    ValueTask<Result<SlackAuth>> AuthTest(string token, CancellationToken cancellationToken);
}
