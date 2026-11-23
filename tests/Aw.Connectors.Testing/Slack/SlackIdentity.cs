namespace Aw.Connectors.Testing.Slack;

/// <summary>Who the token belongs to: what the fake's 'auth.test' answers.</summary>
/// <param name="TeamId">The workspace id ('T…').</param>
/// <param name="Team">The workspace name.</param>
/// <param name="Domain">The workspace's subdomain of slack.com.</param>
/// <param name="UserId">The token's user id ('U…').</param>
/// <param name="User">The token's user handle.</param>
public sealed record SlackIdentity(string TeamId, string Team, string Domain, string UserId, string User);
