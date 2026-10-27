using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// The context engine's doctor rows, after Sindri's own lease, ledger, profile, api and temporal: 'context:map' and
/// 'context:retention' (S-3a), 'context:publish' (F1P-c: is the map held for the code index, overdue, or answering from another commit than the index), 'context:onyx' (S-5A, judged by <see cref="OnyxDoctor"/>) and 'context:linear' (S-6, <see cref="LinearDoctor"/>).
/// </summary>
/// <param name="context">The context engine.</param>
/// <param name="onyx">The onyx check.</param>
/// <param name="publish">The map's publish row.</param>
/// <param name="linear">The linear check.</param>
/// <param name="codeIndex">The code index checks.</param>
internal sealed class ContextEngineChecks(IContextEngine context, OnyxDoctor onyx, IMapPublishCheck publish, LinearDoctor linear, ICodeIndexChecks codeIndex)
{
    /// <summary>The checks, in the order doctor prints them.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads and the probe.</param>
    /// <returns>The five context checks, then the code index's.</returns>
    internal async ValueTask<DoctorCheck[]> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        var scope = ScopeOf(approved);
        return
        [
            Line("context:map", await context.CheckMap(scope, cancellationToken).ConfigureAwait(false)),
            await Roles(scope, cancellationToken).ConfigureAwait(false),
            Line("context:retention", await context.CheckRetention(cancellationToken).ConfigureAwait(false)),
            await publish.Check(approved, cancellationToken).ConfigureAwait(false),
            await onyx.Check(approved, cancellationToken).ConfigureAwait(false),
            await linear.Check(approved, cancellationToken).ConfigureAwait(false),
            .. await codeIndex.Check(approved, cancellationToken).ConfigureAwait(false),
        ];
    }

    // 'context:roles' (issue 1105): read from the map status, so it counts what status counts; nothing to say without an approved profile or a map.
    private async ValueTask<DoctorCheck> Roles(ContextScope? scope, CancellationToken cancellationToken)
    {
        if (scope is null)
        {
            return new DoctorCheck("context:roles", CheckState.Ok, "not configured: no approved profile", Fix: null);
        }

        var status = await context.MapStatus(anchor: null, scope, cancellationToken).ConfigureAwait(false);
        return status.IsError && string.Equals(status.Error.Code, ContextErrors.NoMap.Code, StringComparison.Ordinal)
            ? new DoctorCheck("context:roles", CheckState.Ok, "no map yet", Fix: null)
            : status.IsError ? Line("context:roles", status.Error) : Line("context:roles", RoleHealth.Check(status.Value.Roles ?? []));
    }

    // The approved profile's scope as a service reads it (the deny globs apply); null while no profile is approved.
    private static ContextScope? ScopeOf(LoadedProfile? approved) => approved is null
        ? null
        : new ContextScope(approved.Profile.Context, ScopeRules.Usable([.. (approved.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(approved.Profile, r), [.. RepoConcepts.For(approved.Profile, r).Select(c => ConceptPin.Of(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name, c.Summary, c.Features, c.Paths))], DemotePaths: RepoDemoteGlobs.For(approved.Profile, r)) { Roles = RepoRoleSets.For(approved.Profile, r) is { } sets ? [.. sets.Select(x => new RepoRole(x.Name, x.Demoted, x.Globs))] : null })]), new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));

    private static DoctorCheck Line(string name, Result<MapHealth> health) => health.IsError
        ? new DoctorCheck(name, CheckState.Fail, $"{health.Error.Code}: {health.Error.Message}", "check $AW_SINDRI_HOME/context")
        : new DoctorCheck(health.Value.Name, health.Value.State switch
        {
            MapHealthState.Ok => CheckState.Ok,
            MapHealthState.Warn => CheckState.Warn,
            MapHealthState.Fail => CheckState.Fail,
            _ => CheckState.Fail,
        }, health.Value.Detail, health.Value.Fix);
}
