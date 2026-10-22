namespace Aw.Sindri.Core.Onyx;

/// <summary>The owner's Onyx login, as the app form sent it: held in memory for one connect and dropped.</summary>
/// <param name="Email">The admin account's email.</param>
/// <param name="Password">The password; a secret, never text.</param>
public sealed record OnyxLogin(string Email, OnyxSecret Password)
{
    /// <summary>Prints no part of the login: a record's own formatting would print the email and call the secret's ToString, and neither belongs in a log.</summary>
    /// <returns>A fixed word.</returns>
    public override string ToString() => "(onyx login)";
}
