using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// When a 'wait' node wakes: a duration after now, or an 'until' time. An 'until' is a local date and time ('2026-03-08T09:00'), a bare clock time ('07:00', the next one),
/// or a time with an offset ('2026-03-08T09:00:00Z'); the first two are read in 'timezone' (an IANA name, UTC when missing).
/// A local time the clock skips (spring forward) wakes at the first instant after the gap; one that happens twice (fall back) wakes at the first.
/// </summary>
public static class WaitSchedule
{
    private static readonly string[] _localFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    /// <summary>The instant the wait ends.</summary>
    /// <param name="config">The node's config: 'duration', or 'until' with an optional 'timezone'.</param>
    /// <param name="now">The instant the wait starts.</param>
    /// <returns>The wake-up instant, or why the config can't be read.</returns>
    public static Result<DateTimeOffset> WakeAt(JsonObject config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(config);
        var duration = Text(config["duration"]);
        var until = Text(config["until"]);
        if ((duration is null) == (until is null))
        {
            return SindriErrors.GraphBlockFailed.With("wait needs exactly one of 'duration' and 'until'");
        }

        if (duration is not null)
        {
            var length = BlockDuration.Parse(duration);
            return length.IsError ? length.Error : now + length.Value;
        }

        return Until(until ?? string.Empty, Text(config["timezone"]), now);
    }

    /// <summary>Problems a config has whatever the clock says; references ('{{...}}') are not checked because they have no value yet.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>One problem per field: the field's name and what to change.</returns>
    public static IReadOnlyList<(string Field, string Problem)> Check(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var duration = Text(config["duration"]);
        var until = Text(config["until"]);
        var problems = new List<(string Field, string Problem)>();
        if ((duration is null) == (until is null))
        {
            problems.Add((until is null ? "duration" : "until", "write exactly one of 'duration' and 'until'"));
        }

        AddProblem(problems, "duration", duration, static d => ErrorOf(BlockDuration.Parse(d)));
        AddProblem(problems, "timezone", Text(config["timezone"]), static z => ErrorOf(Zone(z)));
        AddProblem(problems, "until", until, static u => ErrorOf(Until(u, timezone: null, DateTimeOffset.UnixEpoch)));
        return problems;
    }

    private static void AddProblem(List<(string Field, string Problem)> problems, string field, string? value, Func<string, Error?> check)
    {
        if (value is not null && !IsReference(value) && check(value) is { } error)
        {
            problems.Add((field, error.Message));
        }
    }

    private static Error? ErrorOf<T>(Result<T> result) => result.IsError ? result.Error : null;

    private static Result<DateTimeOffset> Until(string until, string? timezone, DateTimeOffset now)
    {
        var text = until.Trim();
        if (text.Contains('Z', StringComparison.Ordinal) || HasOffset(text))
        {
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
                ? instant
                : Bad(until);
        }

        var zone = Zone(timezone ?? "UTC");
        if (zone.IsError)
        {
            return zone.Error;
        }

        if (DateTime.TryParseExact(text, _localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return Resolve(local, zone.Value);
        }

        if (TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock))
        {
            var today = TimeZoneInfo.ConvertTime(now, zone.Value).Date.Add(clock.ToTimeSpan());
            var first = Resolve(today, zone.Value);
            return first > now ? first : Resolve(today.AddDays(1), zone.Value);
        }

        return Bad(until);
    }

    // why: '2026-03-08T09:00+02:00' and '...-07:00' end in an offset; a bare local time has no sign after its 'T'
    private static bool HasOffset(string text)
    {
        var t = text.IndexOf('T', StringComparison.Ordinal);
        return t >= 0 && text.AsSpan(t).IndexOfAny('+', '-') >= 0;
    }

    private static DateTimeOffset Resolve(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            // why: the clock jumps over this time; the first instant after the jump is the local time moved forward by the jump's length
            var jump = zone.GetUtcOffset(unspecified.AddHours(3)) - zone.GetUtcOffset(unspecified.AddHours(-3));
            var moved = unspecified + jump;
            return new DateTimeOffset(moved, zone.GetUtcOffset(moved));
        }

        if (zone.IsAmbiguousTime(unspecified))
        {
            // why: the first time round has the larger offset (it is still summer time)
            return new DateTimeOffset(unspecified, zone.GetAmbiguousTimeOffsets(unspecified).Max());
        }

        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    private static Result<TimeZoneInfo> Zone(string name)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(name);
        }
        catch (TimeZoneNotFoundException)   // boundary: the OS time zone database does not know the name
        {
            return SindriErrors.GraphBlockFailed.With($"'{name}' is not a timezone this machine knows; use an IANA name such as America/Los_Angeles");
        }
        catch (InvalidTimeZoneException)   // boundary: the database entry is broken
        {
            return SindriErrors.GraphBlockFailed.With($"the timezone '{name}' can't be read on this machine; use another IANA name");
        }
    }

    private static Error Bad(string until) =>
        SindriErrors.GraphBlockFailed.With($"'{until}' is not a time; write 2026-03-08T09:00 (local), 07:00 (the next 07:00) or 2026-03-08T09:00:00Z");

    private static bool IsReference(string text) => text.Contains("{{", StringComparison.Ordinal);

    private static string? Text(JsonNode? node) => node is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
}
