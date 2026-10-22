using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a publish job (code index spec 6.4): finishes a held map. It reads the hold and asks <see cref="MapPublishDecision"/> again under its own grant; on publish it runs the cleanup pass once more
/// (a scoping proposal may have joined the draft while it waited) and promotes through the engine, naming the draft it decided about; on drop it releases the hold. SND-CONTEXT-190 (a rebuild replaced the draft)
/// ends the job succeeded with the code and drops the hold; any other error (046: an undecided item a person must decide) fails it and keeps the hold, for the tick's backoff.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="source">Opens the recorder of the map's events under the held lease.</param>
/// <param name="jobs">The running job: its progress takes the step lines and its id keeps one drop's event key apart from another's.</param>
/// <param name="clock">The time.</param>
internal sealed class PublishContextMapService(ContextScopes scopes, IContextEngine engine, IIndexOutlooks outlooks, MapEventSource source, IJobContextAccessor jobs, TimeProvider clock)
    : IService<PublishContextMapRequest, PublishContextMapResult>
{
    public async ValueTask<Result<PublishContextMapResult>> Handle(PublishContextMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var opened = source.Open();
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (opened.IsError || scope.IsError)
        {
            return opened.IsError ? opened.Error : scope.Error;
        }

        var wait = MapGate.Wait(scope.Value);
        var state = await engine.Held(scope.Value, cancellationToken).ConfigureAwait(false);
        if (wait.IsError || state.IsError)
        {
            return wait.IsError ? wait.Error : state.Error;
        }

        if (MapGate.HoldOf(state.Value) is not { } hold)
        {
            return new PublishContextMapResult(Version: 0, "NotHeld", Code: null);
        }

        var read = await outlooks.Read(hold, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var now = clock.GetUtcNow();
        return MapPublishDecision.Decide(MapGate.Input(state.Value, hold, read.Value, wait.Value), now) switch
        {
            MapPublish publish => await Publish(new Run(opened.Value, scope.Value), hold, publish, now, cancellationToken).ConfigureAwait(false),
            MapDrop drop => await Drop(new Run(opened.Value, scope.Value), hold.Version, (drop.Reason.ToString(), Code: null), cancellationToken).ConfigureAwait(false),
            _ => new PublishContextMapResult(hold.Version, "Waiting", Code: null),
        };
    }

    private async ValueTask<Result<PublishContextMapResult>> Publish(Run run, MapHoldState hold, MapPublish publish, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var progress = jobs.Current?.Progress;
        progress?.Report("cleaning up");
        var cleaned = await engine.CleanDraft(run.Scope, cancellationToken, hold.Version).ConfigureAwait(false);
        if (cleaned.IsError)
        {
            return await Refused(run, hold.Version, cleaned.Error, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("promoting");
        var promoted = await engine.Promote(run.Scope, cancellationToken, hold.Version).ConfigureAwait(false);
        if (promoted.IsError)
        {
            return await Refused(run, hold.Version, promoted.Error, cancellationToken).ConfigureAwait(false);
        }

        var version = promoted.Value.Version;
        var appended = await run.Recorder.Promoted(promoted.Value, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var published = await run.Recorder.Published(MapGate.Published(version, publish, "check", hold, now), cancellationToken).ConfigureAwait(false);
        return published.IsError ? published.Error : new PublishContextMapResult(version.Number, publish.Reason.ToString(), Code: null);
    }

    // 190 means another writer replaced the draft: nothing to promote, nothing to raise. Any other refusal is the job's failure, and the hold stays.
    private async ValueTask<Result<PublishContextMapResult>> Refused(Run run, int version, Error error, CancellationToken cancellationToken) =>
        error.Code == ContextErrors.DraftChanged.Code
            ? await Drop(run, version, ("Gone", error.Code), cancellationToken).ConfigureAwait(false)
            : error;

    private async ValueTask<Result<PublishContextMapResult>> Drop(Run run, int version, (string Reason, string? Code) why, CancellationToken cancellationToken)
    {
        var released = await engine.Release(version, run.Scope, cancellationToken).ConfigureAwait(false);
        if (released.IsError)
        {
            return released.Error;
        }

        var repo = run.Scope.Repos.Count > 0 ? run.Scope.Repos[0].Name.Value : "map";
        var attempt = jobs.Current?.Job.Value ?? clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        var appended = await run.Recorder.HoldDropped(repo, version, why.Reason, attempt, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new PublishContextMapResult(version, why.Reason, why.Code);
    }

    private readonly record struct Run(ContextMapEvents Recorder, ContextScope Scope);
}
