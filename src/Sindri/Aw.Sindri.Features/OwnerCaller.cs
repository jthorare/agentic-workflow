using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features;

/// <summary>
/// The check for a command that is the owner's (first-run spec decisions 11 and 13): the caller must have shown the per-start human token, which makes it the <see cref="CallerClass.Human"/> class, as
/// `sindri profile set` requires (Host/Commands/Verbs.cs SetProfileKey). A session Sindri launched never has it, and neither does an agent started without it. The guard is against honest mistakes, not a hostile agent.
/// </summary>
internal static class OwnerCaller
{
    /// <summary>SND-AUTH-002 when <paramref name="callers"/>' caller is not the owner.</summary>
    /// <param name="callers">The caller of the code running now.</param>
    /// <param name="what">The command, in words ("restarting Sindri").</param>
    /// <returns>The refusal, or null for the owner.</returns>
    internal static Error? Refusal(ICallerContextAccessor callers, string what) =>
        callers.Current is { Class: CallerClass.Human }
            ? null
            : SindriErrors.AuthRefused.With($"{what} is for the person at the keyboard (fix: run it from your own terminal or the app)");
}
