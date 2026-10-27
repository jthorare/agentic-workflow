using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Doctor;

/// <summary>The doctor's verdicts (design spec §10.3, §11.5), one pure rule per check; the service only gathers the facts.</summary>
public static class DoctorRules
{
    /// <summary>The lease: free, held by a live process, held by a dead one, or held from another machine.</summary>
    /// <param name="row">The stored lease.</param>
    /// <param name="now">The current time.</param>
    /// <param name="liveness">What is known about the holder.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Lease(LeaseRow? row, DateTimeOffset now, HolderLiveness liveness) => row switch
    {
        null => new("lease", CheckState.Ok, "free (never held)", Fix: null),
        _ when !row.IsLive(now) => new("lease", CheckState.Ok, Text($"free (last epoch {row.Epoch})"), Fix: null),
        _ => liveness switch
        {
            HolderLiveness.Alive => new("lease", CheckState.Ok, Text($"held by {row.Holder} since {row.AcquiredAt:yyyy-MM-ddTHH:mm:ssZ}, epoch {row.Epoch}"), Fix: null),
            HolderLiveness.ProvenDead => new("lease", CheckState.Fail, Text($"held by {row.Holder}, which is no longer running (epoch {row.Epoch})"), "sindri"),
            HolderLiveness.Unknown => new("lease", CheckState.Warn, Text($"held by {row.Holder} on another machine until {row.ExpiresAt:yyyy-MM-ddTHH:mm:ssZ}"), "wait for it to expire, or stop it there"),
            _ => throw new ArgumentOutOfRangeException(nameof(liveness), liveness, "unknown liveness"),
        },
    };

    /// <summary>The ledger's schema against this build.</summary>
    /// <param name="schema">The schema status, or the error reading it.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Ledger(Result<SchemaStatus> schema) => schema switch
    {
        { IsError: true } => new("ledger", CheckState.Fail, schema.Error.Message, "check the file's permissions and disk, then run sindri doctor again"),
        { Value.Applied: 0 } => new("ledger", CheckState.Warn, "no ledger yet", "sindri"),
        { Value.IsCurrent: true } => new("ledger", CheckState.Ok, Text($"schema {schema.Value.Applied}/{schema.Value.Expected}"), Fix: null),
        _ => new("ledger", CheckState.Fail, Text($"schema {schema.Value.Applied}/{schema.Value.Expected}: written by another build"), "use the build that wrote it, or move the ledger aside"),
    };

    /// <summary>The views: caught up, rebuilding, stalled on an event, or not built yet.</summary>
    /// <param name="state">The projector_state row (null when the views store doesn't exist), or why it can't be read.</param>
    /// <param name="head">The ledger's head, or why it can't be read.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Views(Result<ProjectorState?> state, Result<long> head) =>
        state.IsError ? new("views", CheckState.Fail, state.Error.Message, "check the file's permissions and disk, then run sindri doctor again")
        : head.IsError ? new("views", CheckState.Fail, head.Error.Message, "check the file's permissions and disk, then run sindri doctor again")
        : state.Value switch
        {
            null => new("views", CheckState.Warn, "no views yet", "sindri"),
            { StalledAt: { } at } s => new("views", CheckState.Fail, Text($"stalled at {at} ({s.StalledType})"), "sindri views rebuild"),
            { Rebuilding: true } s => new("views", CheckState.Warn, Text($"rebuilding (cursor {s.Cursor} of {head.Value})"), "wait for the rebuild, then run sindri doctor again"),
            var s => new("views", CheckState.Ok, Text($"cursor {s.Cursor} of head {head.Value}"), Fix: null),
        };

    /// <summary>
    /// The profile: valid, and approved by <see cref="ProfileRules.IsApproved"/> (it is the saved approval and the latest
    /// recorded 'profile.approved' event names it). Changed since the saved approval, or saved but not the latest recorded
    /// approval, is a warn whose fix is approving again.
    /// </summary>
    /// <param name="live">The live profile, or why it doesn't load.</param>
    /// <param name="approved">The latest saved approval, or why it can't be read.</param>
    /// <param name="recorded">The latest recorded approval, or why the ledger can't be read.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Profile(Result<LoadedProfile> live, Result<LoadedProfile?> approved, Result<RecordedApproval?> recorded)
    {
        if (live.IsError)
        {
            return new("profile", CheckState.Fail, live.Error.Message, "edit the profile; keys are in docs/csharp/sindri.md");
        }

        if (approved.IsError || recorded.IsError)
        {
            return new("profile", CheckState.Fail, approved.IsError ? approved.Error.Message : recorded.Error.Message,
                "check the file's permissions and disk, then run sindri doctor again");
        }

        return (approved.Value, recorded.Value?.Hash) switch
        {
            (null, _) => new("profile", CheckState.Fail, "not approved", "sindri profile approve"),
            ({ } saved, _) when saved.Hash != live.Value.Hash => new("profile", CheckState.Warn,
                $"changed since approval ({saved.Hash.Abbreviated} → {live.Value.Hash.Abbreviated}); running with the approved one", "sindri profile approve"),
            ({ } saved, var latest) when !ProfileRules.IsApproved(saved.Hash, latest, live.Value.Hash) => new("profile", CheckState.Warn,
                latest is { } other
                    ? $"approval {saved.Hash.Abbreviated} is saved but isn't the latest recorded one ({other.Abbreviated})"
                    : $"approval {saved.Hash.Abbreviated} is saved but not recorded in the ledger", "sindri profile approve"),
            ({ } saved, _) => new("profile", CheckState.Ok, $"approved, hash {saved.Hash.Abbreviated}", Fix: null),
        };
    }

    /// <summary>The API: it answers, at the epoch the lease holds.</summary>
    /// <param name="answer">The endpoint and its ping, or why there is none.</param>
    /// <param name="row">The stored lease.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Api(Result<(ControlEndpoint Endpoint, Ping Ping)> answer, LeaseRow? row, DateTimeOffset now)
    {
        if (answer.IsError)
        {
            return new("api", CheckState.Fail, $"unreachable: {answer.Error.Message}", "sindri");
        }

        var (endpoint, ping) = answer.Value;
        return row is not null && row.IsLive(now) && row.Epoch == ping.Epoch
            ? new("api", CheckState.Ok, Text($"{endpoint.Url.Authority} answers (pid {ping.Pid}, epoch {ping.Epoch})"), Fix: null)
            : new("api", CheckState.Warn, Text($"{endpoint.Url.Authority} answers at epoch {ping.Epoch}, but the lease is at {row?.Epoch ?? 0}"), "sindri stop, then sindri");
    }

    /// <summary>The build: the running control plane is the one 'bin/cs/current' names (#1032).</summary>
    /// <param name="running">The stamp this process runs from, or null when it isn't an installed build.</param>
    /// <param name="installed">The stamp 'bin/cs/current' names, or null.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Build(string? running, string? installed) => StaleBuild.Is(running, installed)
        ? new("build", CheckState.Warn, StaleBuild.Sentence(running!, installed!), StaleBuild.Fix)   // why: Is is true only when both are known
        : new("build", CheckState.Ok, running is null ? "not running from an installed build" : $"serving {running}", Fix: null);

    /// <summary>
    /// The Temporal dev server (engine spec rev 8 §0 "Operations"): it runs on this home's database, answers on loopback,
    /// is the version binaries.lock pins, and has the 'sindri' namespace.
    /// </summary>
    /// <param name="facts">What the probe found.</param>
    /// <returns>The check.</returns>
    public static DoctorCheck Temporal(TemporalFacts facts)
    {
        if (facts.Running is not { } s)
        {
            return new("temporal", CheckState.Fail, "not running: no dev server on this home's temporal.db", "sindri");
        }

        var at = Text($"pid {s.Pid} on 127.0.0.1:{s.Port}");
        if (!facts.Reachable)
        {
            return new("temporal", CheckState.Fail, $"{at} doesn't answer: {facts.Problem ?? "no answer"}", "sindri stop, then sindri");
        }

        if (!string.Equals(facts.Version, facts.Pinned, StringComparison.Ordinal))
        {
            return new("temporal", CheckState.Fail, $"{at} is version {facts.Version ?? "unknown"}, but binaries.lock pins {facts.Pinned}",
                "scripts/binaries-lock.sh fetch temporal, then sindri stop and sindri");
        }

        return facts.NamespacePresent
            ? new("temporal", CheckState.Ok, Text($"127.0.0.1:{s.Port} answers (pid {s.Pid}), version {facts.Version} as pinned, namespace sindri present"), Fix: null)
            : new("temporal", CheckState.Fail, $"{at} answers, but has no namespace sindri", "sindri stop, then sindri");
    }

    /// <summary>The exit code for a set of checks: 0 all ok, 1 any warn, 2 any fail.</summary>
    /// <param name="checks">The checks.</param>
    /// <returns>The exit code.</returns>
    public static int ExitCode(IEnumerable<DoctorCheck> checks)
    {
        var states = checks.Select(c => c.State).ToList();
        return states.Contains(CheckState.Fail) ? 2 : states.Contains(CheckState.Warn) ? 1 : 0;
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
