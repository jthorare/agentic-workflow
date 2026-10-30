namespace Aw.Sindri.Core.Profile;

/// <summary>The words for a profile that lists Linear teams but names no token, so nothing syncs (issue #1739): the Projects page and doctor's 'context:linear' row say the same thing.</summary>
public static class LinearToken
{
    /// <summary>The sentence's start: Linear needs a token.</summary>
    public const string Missing = "Linear needs a token";

    /// <summary>The command that connects Linear (stores the token the profile points at).</summary>
    public const string Connect = "sindri index connect linear";
}
