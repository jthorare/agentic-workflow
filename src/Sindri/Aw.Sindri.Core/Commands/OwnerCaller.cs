using Aw.Kernel;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// The check for a command that is the owner's (first-run spec decisions 11, 13 and 29): the caller must have shown the per-start human token, which makes it the <see cref="CallerClass.Human"/> class, as
/// `sindri profile set` requires (Host/Commands/Verbs.cs SetProfileKey). Only a session Sindri launched is refused for sure: it carries a launch secret, so the host makes it a session whatever token it sends
/// (Host/CallerContextMiddleware.cs AsLaunched). An agent the owner started in their own terminal can read the token, so it counts as the owner. The guard is against honest mistakes, not a hostile agent.
/// </summary>
public static class OwnerCaller
{
    /// <summary>SND-AUTH-002 when <paramref name="callers"/>' caller is not the owner.</summary>
    /// <param name="callers">The caller of the code running now.</param>
    /// <param name="what">The command, in words ("restarting Sindri").</param>
    /// <returns>The refusal, or null for the owner.</returns>
    public static Error? Refusal(ICallerContextAccessor callers, string what) =>
        callers.Current is { Class: CallerClass.Human }
            ? null
            : SindriErrors.AuthRefused.With($"{what} is for the person at the keyboard (fix: run it from your own terminal or the app)");
}
