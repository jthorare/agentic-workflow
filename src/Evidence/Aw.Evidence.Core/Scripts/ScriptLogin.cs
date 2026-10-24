using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Core.Scripts;

/// <summary>
/// A script's own sign-in, run before the first step: environment variable names and CSS selectors only, never a credential.
/// A one-step email and password form is the default, so the two variable names alone are enough for one (the run fills in the selectors).
/// </summary>
/// <param name="EmailEnv">The environment variable holding the email (UPPER_SNAKE).</param>
/// <param name="PasswordEnv">The environment variable holding the password (UPPER_SNAKE).</param>
/// <param name="EmailSelector">The email field; default 'input[type="email"]'.</param>
/// <param name="PasswordSelector">The password field; default 'input[type="password"]'.</param>
/// <param name="SubmitSelector">The submit control; default 'button[type="submit"]'.</param>
/// <param name="NextSelector">Clicked after the email on two-step forms.</param>
/// <param name="LoginPath">Sign-in is done once the URL path no longer contains this; default '/login'.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ScriptLogin(
    string EmailEnv,
    string PasswordEnv,
    string? EmailSelector = null,
    string? PasswordSelector = null,
    string? SubmitSelector = null,
    string? NextSelector = null,
    string? LoginPath = null)
{
    /// <summary>The rules this login breaks.</summary>
    /// <returns>One message per problem; empty when it is well formed.</returns>
    public IEnumerable<string> Problems()
    {
        if (!EnvName.IsMatch(EmailEnv))
        {
            yield return "login.emailEnv must be an UPPER_SNAKE environment variable name";
        }

        if (!EnvName.IsMatch(PasswordEnv))
        {
            yield return "login.passwordEnv must be an UPPER_SNAKE environment variable name";
        }

        if (new[] { EmailSelector, PasswordSelector, SubmitSelector, NextSelector, LoginPath }.Any(static v => v is { Length: 0 }))
        {
            yield return "login selectors and loginPath must not be empty";
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex EnvName { get; }
}
