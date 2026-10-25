using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>'context:publish' (F1P-c): is the map held for the code index, overdue, or answering from another commit than the index.</summary>
/// <param name="publish">The map's publish status.</param>
/// <param name="clock">The time.</param>
internal sealed class MapPublishDoctorCheck(MapPublishStatusReader publish, TimeProvider clock) : IMapPublishCheck
{
    /// <summary>The name of the publish row.</summary>
    internal const string PublishName = "context:publish";

    private static readonly TimeSpan _staleGrace = TimeSpan.FromMinutes(5);

    // Held inside its wait and every published state read as ok; overdue (the map service is paused or the publish is failing) and code answers from another commit than the index warn.
    public async ValueTask<DoctorCheck> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (ScopeOf(approved) is not { } scope)
        {
            return new DoctorCheck(PublishName, CheckState.Ok, "no approved profile, so no map to publish", Fix: null);
        }

        var status = await publish.Read(scope, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return status.Error.Code == ContextErrors.NoMap.Code
                ? new DoctorCheck(PublishName, CheckState.Ok, "no map yet, so nothing to publish", Fix: null)
                : new DoctorCheck(PublishName, CheckState.Warn, $"{status.Error.Code}: {status.Error.Message}", "sindri context map status");
        }

        var view = status.Value;
        var last = view.Reason is { } reason ? $" (last publish: {reason})" : string.Empty;
        return view.State switch
        {
            MapPublishStatusRules.Held => new DoctorCheck(PublishName, CheckState.Ok, string.Create(CultureInfo.InvariantCulture, $"v{view.Version} held until {view.Until:HH:mm}Z"), Fix: null),
            MapPublishStatusRules.Overdue => new DoctorCheck(PublishName, CheckState.Warn, view.Lines[0], "sindri services resume context-map-refresh, then sindri jobs --kind PublishContextMap"),
            MapPublishStatusRules.PublishedStale when StillStale(view) => new DoctorCheck(PublishName, CheckState.Warn, FirstLine(view) + last, "sindri code-index status; it clears when the index reaches the map's commit"),
            _ => new DoctorCheck(PublishName, CheckState.Ok, FirstLine(view) + last, Fix: null),
        };
    }

    // More than five minutes after the publish the rows should have caught up; inside that window the index is just a little ahead.
    private bool StillStale(MapPublishStatusView view) => view.PublishedAt is not { } at || clock.GetUtcNow() - at > _staleGrace;

    private static string FirstLine(MapPublishStatusView view) => view.Lines.Count > 0 ? view.Lines[0] : view.Word;

    // The approved profile's scope as a service reads it (the deny globs apply); null while no profile is approved.
    private static ContextScope? ScopeOf(LoadedProfile? approved) => approved is null
        ? null
        : new ContextScope(approved.Profile.Context, ScopeRules.Usable(ContextScopes.Entries(approved.Profile)), new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service));
}
