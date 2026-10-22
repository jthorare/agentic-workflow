using System.Globalization;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// How the window words Sindri's answers. Presentation only: every verdict (running, needs approval, changed) comes from
/// Sindri's status record; this picks the words, the same words 'sindri status' uses.
/// </summary>
internal static class StatusText
{
    internal const string ApproveFix = "sindri profile approve";

    // The health line (§4.8): Running · pid · epoch · API v1 · N services · profile <state>, the fields the status carries.
    internal static string HealthLine(StatusView s) => string.Join(" · ", HealthWords(s));

    internal static string Lease(StatusView s) => string.Create(CultureInfo.InvariantCulture, $"pid {s.Pid} · epoch {s.Epoch}");

    // "approved" only when Sindri says nothing needs approval; otherwise the most specific reason the record gives.
    internal static string ProfileState(ProfileView p) => p switch
    {
        { Approved: null, Problem: not null } => "unreadable",
        { Approved: null } => "not approved",
        { ChangedSinceApproval: true } => "changed since approval",
        { NeedsApproval: true } => "needs approval",
        _ => "approved",
    };

    internal static string ProfileDetail(ProfileView p) => p switch
    {
        { Approved: null, Problem: { } problem } => problem,
        { Approved: null } => $"{p.Source} (hash {Short(p.Live)})",
        { ChangedSinceApproval: true } => $"running with approved {Short(p.Approved)}; {p.Source} is now {Short(p.Live)}",
        { Problem: { } problem } => $"hash {Short(p.Approved)}; the live file has a problem: {problem}",
        _ => $"hash {Short(p.Approved)} · {(p.Source is { } source ? Home(source) : null)}",
    };

    // Approving needs a live profile that loads: offer the command only when it can work (approve refuses an unreadable one).
    internal static string ProfileFix(ProfileView p) => p.NeedsApproval && p.Live is not null ? ApproveFix : string.Empty;

    internal static ServiceRow Service(ServiceView s) => new(s.Name, "every " + s.Every, s.LastRun is { } t ? "last run " + Time(t) : "not run yet");

    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture, $"{e.Type} · {Time(e.OccurredAt)} · #{e.Sequence}");

    // Home's strip (§4.2, §4.8): the health line's fields as labelled cells, each value a word.
    internal static IEnumerable<HealthCell> Strip(StatusView s)
    {
        yield return new(Labels.CellProcess, string.Create(CultureInfo.InvariantCulture, $"{Labels.Running} · pid {s.Pid}"));
        yield return new(Labels.CellEpoch, string.Create(CultureInfo.InvariantCulture, $"{s.Epoch} · {Labels.LeaseHeld}"));
        if (s.ApiVersion is { } version)
        {
            yield return new(Labels.CellApi, string.Create(CultureInfo.InvariantCulture, $"v{version}"));
        }

        yield return new(Labels.CellServices, Services(s));
        yield return new(Labels.CellProfile, ProfileHead(s.Profile), ProfileGlyph(s.Profile));
    }

    // The Health page's process rows (§4.8): pid and epoch, the lease and its expiry, the API's address and version.
    internal static IEnumerable<HealthCell> Process(Uri url, StatusView s)
    {
        yield return new(Labels.RowPidEpoch, string.Create(CultureInfo.InvariantCulture, $"{s.Pid} · {s.Epoch}"));
        yield return new(Labels.RowLease, s.LeaseExpiresAt is { } expires ? $"{Labels.LeaseHeld} · expires {Time(expires)}" : Labels.LeaseHeld);
        yield return new(Labels.CellApi, s.ApiVersion is { } version ? string.Create(CultureInfo.InvariantCulture, $"{url.Authority} · v{version}") : url.Authority);
    }

    // The profile's state in words; its glyph is ProfileGlyph's.
    internal static string ProfileHead(ProfileView p) => ProfileState(p);

    // The profile state's §11.3 glyph: ✓ approved, ✕ unreadable, ◇ anything else to look at.
    internal static StateGlyph ProfileGlyph(ProfileView p) => ProfileState(p) switch
    {
        "approved" => StateGlyph.Ok,
        "unreadable" => StateGlyph.Down,
        _ => StateGlyph.Profile,
    };

    // The Health page's profile rows (§4.8): the approved hash, the live one when it differs, the file with ~ for home.
    internal static IEnumerable<HealthCell> Profile(ProfileView p)
    {
        yield return new(Labels.RowApproved, Short(p.Approved));
        if (!string.Equals(p.Live, p.Approved, StringComparison.Ordinal))
        {
            yield return new(Labels.RowLive, Short(p.Live));
        }

        if (p.Source is { } source)
        {
            yield return new(Labels.RowFile, ShortPath(source), IsPath: true);
        }
    }

    /// <summary>The longest path <see cref="ShortPath"/> shows.</summary>
    internal const int ShortPathLength = 48;

    // A path as the app shows it (I5, I7): ~ for home, then, past ShortPathLength, its first segment, an ellipsis and as
    // many whole trailing segments as fit; a file name longer than that keeps its end.
    internal static string ShortPath(string path)
    {
        var whole = Home(path);
        if (whole.Length <= ShortPathLength)
        {
            return whole;
        }

        var separator = whole.IndexOf('/', 1);
        var head = (separator > 0 ? whole[..separator] : string.Empty) + "/…";
        var room = ShortPathLength - head.Length;
        var tail = whole[(whole.LastIndexOf('/') + 1)..];
        if (tail.Length + 1 > room)
        {
            return head + "/" + tail[^(room - 1)..];
        }

        foreach (var segment in whole.Split('/').Reverse().Skip(1))
        {
            if (segment.Length + 1 + tail.Length + 1 > room)
            {
                break;
            }

            tail = segment + "/" + tail;
        }

        return head + "/" + tail;
    }

    // A path under the user's home directory, written from ~.
    internal static string Home(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "~" + path[home.Length..] : path;
    }

    private static string Services(StatusView s) =>
        s.Services.Count == 1 ? "1 service" : string.Create(CultureInfo.InvariantCulture, $"{s.Services.Count} services");

    private static IEnumerable<string> HealthWords(StatusView s)
    {
        yield return Labels.Running;
        yield return Lease(s);
        if (s.ApiVersion is { } version)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"API v{version}");
        }

        yield return Services(s);
        yield return "profile " + ProfileState(s.Profile);
    }

    // Times in the viewer's local time, as the mockups show them (§4.8).
    internal static string Time(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Short(string? hash) => hash is { Length: >= 6 } ? hash[..6] : hash ?? "none";
}
