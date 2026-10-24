using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Features.RunScript;

/// <summary>A script's login as the browser signs in with it: the form's selectors default to a one-step email and password form.</summary>
internal static class ScriptLoginSpec
{
    internal static LoginSpec For(ScriptLogin login) => new(
        login.EmailEnv,
        login.PasswordEnv,
        login.EmailSelector ?? "input[type=\"email\"]",
        login.PasswordSelector ?? "input[type=\"password\"]",
        login.SubmitSelector ?? "button[type=\"submit\"]",
        login.NextSelector,
        login.LoginPath);
}
