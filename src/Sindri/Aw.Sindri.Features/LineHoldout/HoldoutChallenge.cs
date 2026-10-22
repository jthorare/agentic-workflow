using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.LineHoldout;

/// <summary>The challenge a protected show issued: the human types <paramref name="Action"/> back at a terminal before it expires.</summary>
/// <param name="Challenge">Its id, sent back with the typed text.</param>
/// <param name="Action">The text to type.</param>
/// <param name="ExpiresAt">When it stops being accepted.</param>
internal sealed record HoldoutChallenge(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt);
