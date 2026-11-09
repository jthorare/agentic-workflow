using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>The challenge the human types back (engine spec §4.2).</summary>
/// <param name="Challenge">The challenge's id.</param>
/// <param name="Action">The action text to type back.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
public sealed record IssueChallengeResponse(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt)
{
    /// <summary>How the app confirms an app challenge: 'typed' (setup-once spec §4 item 2); null on a terminal challenge.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Confirm { get; init; }

    /// <summary>The shortest hold in milliseconds of a held challenge; null (no challenge is held now).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MinHoldMs { get; init; }

    /// <summary>The text that redeems a held challenge instead of a hold; null (no challenge is held now).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TypedAlternative { get; init; }
}
