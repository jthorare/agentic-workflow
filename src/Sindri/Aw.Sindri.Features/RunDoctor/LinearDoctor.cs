using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's 'context:linear' check (context spec §8.8, D-6): whether the approved profile names the native Linear source, which teams it reads, and whether
/// 'context.sources.linear.token' resolves through <paramref name="secrets"/> (a pointer that doesn't fails with its SND-SECRET code, which names the scheme and the
/// target, never a value). It makes no call to Linear: reading projects, documents and issues is 'sindri context sources status'.
/// A profile that lists teams but names no Linear token warns 'Linear needs a token' and names 'sindri index connect linear'; a profile with no Linear entry, or no approved profile, is 'ok: not configured'.
/// </summary>
/// <param name="engine">The context engine (checks the settings).</param>
/// <param name="secrets">Resolves the key's pointer, as Sindri Host does for the engine.</param>
internal sealed class LinearDoctor(IContextEngine engine, ISecretPointers secrets)
{
    /// <summary>The check's name.</summary>
    internal const string Name = "context:linear";

    private const string Setup = "docs/sindri/linear.md";

    /// <summary>Judges the linear source the approved profile names.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The check.</returns>
    internal ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Judge(approved));
    }

    private DoctorCheck Judge(LoadedProfile? approved)
    {
        if (approved is null)
        {
            return new DoctorCheck(Name, CheckState.Ok, "not configured (no profile is approved)", Fix: null);
        }

        ProfileRepoEntry[] entries = [.. (approved.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(approved.Profile.Context, entries);
        if (settings.IsError)
        {
            return new DoctorCheck(Name, CheckState.Fail, $"{settings.Error.Code}: {settings.Error.Message}", "sindri profile validate");
        }

        if (settings.Value.Linear is not { } linear)
        {
            if (settings.Value.LinearTeams.Count > 0)
            {
                return new DoctorCheck(Name, CheckState.Warn,
                    $"{LinearToken.Missing}: the approved profile lists teams {string.Join(", ", settings.Value.LinearTeams)} but no context.sources.linear.token", LinearToken.Connect);
            }

            return new DoctorCheck(Name, CheckState.Ok, "not configured (the approved profile names no context.sources.linear.token)", Fix: null);
        }

        if (ScopeRules.Usable(entries).Count == 0)
        {
            return new DoctorCheck(Name, CheckState.Warn, "the approved profile maps no repo, so no context source runs, linear included", "map a repo in the profile's repos and context.repos");
        }

        var key = SecretPointer.Parse(linear.Token).Bind(secrets.Resolve);
        if (key.IsError)
        {
            return new DoctorCheck(Name, CheckState.Fail, $"{key.Error.Code}: {key.Error.Message}",
                $"fix context.sources.linear.token's target as the message says, or point it at a read-only Linear API key ({Setup})");
        }

        var teams = linear.Teams.Count == 0 ? "every team the key can see" : "teams " + string.Join(", ", linear.Teams);
        return new DoctorCheck(Name, CheckState.Ok, $"configured: {teams}; {linear.Token} resolves (not probed: sindri context sources status reads Linear)", Fix: null);
    }
}
