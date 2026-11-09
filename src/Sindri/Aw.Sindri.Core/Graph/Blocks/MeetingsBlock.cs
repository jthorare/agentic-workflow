using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'meetings' block (#2258, PR 10d), picker label Meetings, group Data: reads one day's meetings from a meeting-notes connection through the
/// <see cref="IMeetingSource"/> of its preset. The day is read in the profile timezone; notes and transcripts are fenced as untrusted in the outputs.
/// </summary>
/// <param name="sources">One source per note app.</param>
/// <param name="model">The fence for untrusted text.</param>
/// <param name="clock">The clock 'today' is read from.</param>
/// <param name="zone">The profile timezone the day is read in.</param>
public sealed class MeetingsBlock(IEnumerable<IMeetingSource> sources, IGraphModelPort model, TimeProvider clock, TimeZoneInfo zone) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["meetings"] = "array", ["count"] = "number" });

    private readonly List<IMeetingSource> _sources = [.. sources];

    /// <summary>The picker label.</summary>
    public static string Title => "Meetings";

    /// <summary>The picker group.</summary>
    public static string Group => "Data";

    /// <inheritdoc />
    public string Type => "meetings";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("connection", BlockFieldType.Text),
        new("day", BlockFieldType.Text, Required: false),
        new("minMinutes", BlockFieldType.Number, Required: false),
        new("include", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"connection":"granola:work","day":"today","minMinutes":10,"include":"notes"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (MeetingsConfig.Problem(config) is { } problem)
        {
            return [(problem.Field, problem.Text)];
        }

        var service = ConnectionName.Parse(config["connection"]?.GetValue<string>() ?? string.Empty).Value.Service;
        return _sources.Exists(s => string.Equals(s.Preset, service, StringComparison.Ordinal))
            ? []
            : [("connection", $"'{service}' is not a meeting-notes connection; use one made from: {string.Join(", ", _sources.Select(static s => s.Preset))}")];
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var config = MeetingsConfig.Parse(context.Config);
        if (config.IsError)
        {
            return config.Error;
        }

        var (connection, day, minMinutes, include) = config.Value;
        if (_sources.Find(s => string.Equals(s.Preset, connection.Service, StringComparison.Ordinal)) is not { } source)
        {
            return SindriErrors.GraphBlockFailed.With($"meetings: '{connection}' is not a meeting-notes connection; use one made from: {string.Join(", ", _sources.Select(static s => s.Preset))}");
        }

        var date = day ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var from = StartOf(date);
        var end = StartOf(date.AddDays(1));
        var read = await source.Read(connection, from, end, include == MeetingInclude.Transcript, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var kept = read.Value
            .Where(m => m.Start >= from && m.Start < end && (m.End - m.Start).TotalMinutes >= minMinutes)
            .OrderBy(static m => m.Start)
            .Select(m => Item(m, include))
            .ToArray();
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["meetings"] = new JsonArray(kept), ["count"] = kept.Length });
    }

    private DateTimeOffset StartOf(DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // why: a zone whose clocks skip midnight has no 00:00 that day; the first valid time is an hour later
        var local = zone.IsInvalidTime(midnight) ? midnight.AddHours(1) : midnight;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private JsonObject Item(Meeting meeting, MeetingInclude include)
    {
        var json = new JsonObject
        {
            ["title"] = meeting.Title,
            ["start"] = Moment(meeting.Start),
            ["end"] = Moment(meeting.End),
            ["attendees"] = new JsonArray([.. meeting.Attendees.Select(static a => (JsonNode?)JsonValue.Create(a))]),
            ["notes"] = model.Untrusted(meeting.Notes),
            ["link"] = meeting.Link,
            ["source"] = meeting.Source,
        };
        if (include == MeetingInclude.Transcript && meeting.Transcript is { } transcript)
        {
            json["transcript"] = model.Untrusted(transcript);
        }

        return json;
    }

    private string Moment(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
