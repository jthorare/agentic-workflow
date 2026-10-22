using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The Features side of the map publish gate (code index spec 6.4): reads the profile's wait, maps Context's stored hold and heads to the Core types the decision takes, and builds the 'published' payload.</summary>
internal static class MapGate
{
    /// <summary>The wait the profile asks for ('context.publish.waitForIndexMinutes'; the default when the section is missing).</summary>
    internal static Result<TimeSpan> Wait(ContextScope scope)
    {
        var settings = SettingsReader.Read(scope.Settings);
        return settings.IsError ? settings.Error : TimeSpan.FromMinutes(settings.Value.Publish.WaitForIndexMinutes);
    }

    /// <summary>The Core view of a stored hold.</summary>
    internal static MapHoldState StateOf(MapHold hold) =>
        new(hold.Version, hold.Since, hold.Heads.ToDictionary(static h => h.Repo.Value, static h => new MapRepoHead(h.Commit, h.OnDefault), StringComparer.Ordinal));

    /// <summary>The Core view of the stored publish state's hold, or null when nothing is held.</summary>
    internal static MapHoldState? HoldOf(MapPublishState state) => state.Hold is null ? null : StateOf(state.Hold);

    /// <summary>The commit by repo name of the held draft or the current version, as <see cref="MappedHeads"/> reads them: the hold's if there is one, else the current version's.</summary>
    internal static IReadOnlyDictionary<string, string>? Mapped(MapPublishState state) =>
        MappedHeads.Of(HoldOf(state), state.CurrentHeads.ToDictionary(static h => h.Repo.Value, static h => h.Commit, StringComparer.Ordinal));

    /// <summary>The decision's input from the stored state and the outlooks read for <paramref name="hold"/>.</summary>
    internal static MapPublishInput Input(MapPublishState state, MapHoldState hold, IReadOnlyDictionary<string, IndexOutlookDetail> outlooks, TimeSpan wait) =>
        new(hold, state.LatestDraft, state.Current, outlooks, wait);

    /// <summary>The 'published' payload of a map that went ahead.</summary>
    internal static ContextMapPublished Published(MapVersion version, MapPublish publish, string via, MapHoldState hold, DateTimeOffset now) =>
        new(version.Repo.Value, version.Number, publish.Reason.ToString(), via, hold.Heads.Count, publish.StaleRepos.Count, (int)Math.Max(0, (now - hold.Since).TotalSeconds),
            hold.Heads.ToDictionary(static h => h.Key, static h => h.Value.Commit, StringComparer.Ordinal));
}
