namespace Aw.Meta.Core.Config;

/// <summary>The team's reviewer lane: when set, a pull request is mergeable on that lane's OK at the head plus the author lane's counts, with no gate run.</summary>
/// <param name="Lane">A lane name from the team's lanes; its session name is "team-lane".</param>
public sealed record ReviewConfig(string Lane);
