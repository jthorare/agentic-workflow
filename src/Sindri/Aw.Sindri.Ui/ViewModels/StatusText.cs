using System.Globalization;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// How the window words Sindri's answers. Presentation only: every verdict (running, health) comes from
/// Sindri's status record; this picks the words, the same words 'sindri status' uses.
/// </summary>
internal static class StatusText
{
    // The health line (§4.8): Running · pid · epoch · API v1 · N services · profile <state>, the fields the status carries.
    internal static string HealthLine(StatusView s) => string.Join(" · ", HealthWords(s));

    internal static string Lease(StatusView s) => string.Create(CultureInfo.InvariantCulture, $"pid {s.Pid} · epoch {s.Epoch}");

    // "unreadable" when the live file doesn't load, else "live": an edit applies on save, so there is no approval state to word.
    internal static string ProfileState(ProfileView p) => IsMissing(p) ? Labels.ProfileNotSetUp : p.Problem is not null ? "unreadable" : "live";

    /// <summary>True when there is no profile yet (a brand-new home), which is not a fault. Only Sindri's problemKind "missing" says it: without it a problem reads as unreadable, so a broken profile file never hides behind a Setup link.</summary>
    /// <param name="p">The status's profile part.</param>
    /// <returns>True when the profile is missing.</returns>
    internal static bool IsMissing(ProfileView p) => string.Equals(p.ProblemKind, "missing", StringComparison.Ordinal);

    internal static string ProfileDetail(ProfileView p) => p switch
    {
        _ when IsMissing(p) => Labels.ProfileNotSetUpDetail,
        { Problem: { } problem } => problem,
        _ => $"hash {Short(p.Live)} · {(p.Source is { } source ? Home(source) : null)}",
    };

    internal static ServiceRow Service(ServiceView s) => new(s.Name, "every " + s.Every, s.LastRun is { } t ? "last run " + Time(t) : "not run yet");

    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture, $"{e.Type} · {Time(e.OccurredAt)} · #{e.Sequence}");

    // Home's strip (§4.2, §4.8): status dots with a word each. pid, epoch, lease and the API version are the System page's (Process).
    internal static IEnumerable<HealthCell> Strip(StatusView s)
    {
        yield return new(Labels.CellProcess, Labels.Running, StateGlyph.Ok);
        if (s.Views is { Stalled: true } views)
        {
            yield return new(Labels.CellViews, Labels.ViewsStalledAt(views.ProjectedAt is { } at ? at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) : null), StateGlyph.Warning, IsAttention: true);
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

    // The profile state's §11.3 glyph: ✓ live, ✕ unreadable, none (a neutral dot) when there is no profile yet.
    internal static StateGlyph ProfileGlyph(ProfileView p) => IsMissing(p) ? StateGlyph.None : p.Problem is not null ? StateGlyph.Down : StateGlyph.Ok;

    // The Health page's profile rows (§4.8): the live hash, the file with ~ for home.
    internal static IEnumerable<HealthCell> Profile(ProfileView p)
    {
        if (!IsMissing(p))
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
    internal static string Clock(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    internal static string Time(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Short(string? hash) => hash is { Length: >= 6 } ? hash[..6] : hash ?? "none";
}
