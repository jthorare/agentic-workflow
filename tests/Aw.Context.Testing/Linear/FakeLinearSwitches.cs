using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// The ways the fake Linear misbehaves (spec D15). Set them at any time from the test; the one-shot ones are used up by the
/// request they act on.
/// </summary>
public sealed class FakeLinearSwitches
{
    /// <summary>One-shot. The next read first applies a teammate's edit to the issue written last (its estimate goes up by one and its updatedAt moves), so the answer holds a field the client never wrote.</summary>
    public bool AnswerWithNewerField { get; set; }

    /// <summary>Every request is rate limited, as an HTTP 429 or as a GraphQL RATELIMITED error, until set back to None.</summary>
    public LinearRateLimit RateLimit { get; set; }

    /// <summary>Every request is refused with 401 from now on, as if the key was revoked.</summary>
    public bool RevokeKey { get; set; }

    /// <summary>Sticky. issueCreate and commentCreate that carry a client id are refused as a repeated id even when the id is fresh.</summary>
    public bool RepeatClientId { get; set; }

    /// <summary>One-shot. The next mutation is applied, then the connection is dropped without a reply: the client can't know the outcome.</summary>
    public bool NoAnswer { get; set; }

    /// <summary>One-shot. The key of a team the issue written last moves to on the next read (new identifier, same id; the old identifier still resolves). Null leaves it.</summary>
    public string? MoveTeam { get; set; }
}
