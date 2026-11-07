using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Doctor's 'library' row (PR review page spec 2026-11-04 §4.1, §5.2): how many design docs the roots hold, a root that can't be read with its path and why (a warning: nothing is removed from it
/// until a later pass lists it again), and the files that look like design docs but aren't read (<c language="text">aw-doc:</c> present but not the number 1). It reads what the library feed's last pass left
/// (<see cref="IContextLibrary.Roots"/>) and reads no folder itself. With no approved profile the row is 'ok: not configured'.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="scopes">The engine's scope for the read.</param>
/// <param name="engine">The context engine.</param>
internal sealed class LibraryDoctorChecks(IApprovedProfile approvals, ContextScopes scopes, IContextEngine engine) : IDoctorChecks
{
    internal const string Name = "library";

    private const string Sync = "sindri library sync";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var scope = latest.IsError || latest.Value is null ? null : await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope is not { IsSuccess: true } current)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "not configured (no profile is approved)", Fix: null)];
        }

        var roots = await engine.Roots(current.Value, cancellationToken).ConfigureAwait(false);
        if (roots.IsError)
        {
            return [string.Equals(roots.Error.Code, ContextErrors.SessionsDisabled.Code, StringComparison.Ordinal)
                ? new DoctorCheck(Name, CheckState.Ok, "not shown to an agent session", Fix: null)
                : new DoctorCheck(Name, CheckState.Warn, $"{roots.Error.Code}: {roots.Error.Message}", Sync)];
        }

        return [Judge([.. roots.Value.Where(static r => r.State != LibraryRootState.Off)])];
    }

    private static DoctorCheck Judge(List<LibraryRootStatus> roots)
    {
        if (roots.Count == 0)
        {
            return new DoctorCheck(Name, CheckState.Ok, "off: context.sources.library.stateDir is off and no vault is named", Fix: null);
        }

        var broken = roots.Where(static r => r.State == LibraryRootState.Unreadable).ToList();
        if (broken.Count > 0)
        {
            return new DoctorCheck(Name, CheckState.Warn, string.Join("; ", broken.Select(static r => $"{r.Root} can't be read: {r.Reason}")),
                "mount or fix the folder, or take the vault out of context.sources.library.vaults; then " + Sync);
        }

        var missed = roots.Where(static r => r.NearMisses > 0).ToList();
        if (missed.Count > 0)
        {
            return NearMisses(missed);
        }

        var waiting = roots.Where(static r => r.State == LibraryRootState.Reading).Select(static r => r.Root).ToList();
        var read = roots.Count - waiting.Count;
        var wait = waiting.Count == 0 ? string.Empty : $"the first read of {string.Join(", ", waiting)} hasn't finished yet ({Sync} reads now)";
        return read == 0
            ? new DoctorCheck(Name, CheckState.Ok, wait, Fix: null)
            : new DoctorCheck(Name, CheckState.Ok, Docs(roots) + (wait.Length == 0 ? string.Empty : "; " + wait), Fix: null);
    }

    private static string Docs(List<LibraryRootStatus> roots)
    {
        var read = roots.Where(static r => r.State == LibraryRootState.Ok).ToList();
        var docs = read.Sum(static r => r.Docs);
        return string.Create(CultureInfo.InvariantCulture, $"{docs} design {(docs == 1 ? "doc" : "docs")} in {read.Count} {(read.Count == 1 ? "root" : "roots")} ({string.Join(", ", read.Select(static r => $"{r.Root} {r.Docs}"))})");
    }

    private static DoctorCheck NearMisses(List<LibraryRootStatus> missed)
    {
        var count = missed.Sum(static r => r.NearMisses);
        var which = string.Join(", ", missed.Select(static r => string.Create(CultureInfo.InvariantCulture, $"{r.Root} {r.NearMisses}")));
        var what = count == 1 ? "1 file looks like a design doc but isn't read" : string.Create(CultureInfo.InvariantCulture, $"{count} files look like design docs but aren't read");
        return new DoctorCheck(Name, CheckState.Warn, $"{what} ({which})", "write aw-doc: 1, unquoted, on the line after the first --- of each file, then " + Sync);
    }
}
