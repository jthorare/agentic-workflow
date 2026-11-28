using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'schedule' trigger: a cron and a timezone; hands on the tick it fired for.</summary>
public sealed class ScheduleBlock : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["scheduledFor"] = "string", ["localDate"] = "string", ["weekday"] = "string" });

    // why: the one-click cron fills; any other cron is still allowed. Each has the words its node's summary line uses.
    private static readonly (string Cron, string Label, string Runs)[] _presets =
    [
        ("0 7 * * 1-5", "Every weekday at 7:00", "every weekday at 7:00"),
        ("0 9 * * *", "Every day at 9:00", "every day at 9:00"),
        ("0 * * * *", "Every hour", "every hour"),
        ("0 8 * * 1", "Every Monday at 8:00", "every Monday at 8:00"),
    ];

    // why: a fixed list of IANA ids, so the block listing is the same on every box (its API golden too); GetSystemTimeZones differs by OS and tzdata version
    private static readonly string[] _zones =
    [
        "UTC", "America/Anchorage", "America/Chicago", "America/Denver", "America/Halifax", "America/Los_Angeles", "America/Mexico_City", "America/New_York",
        "America/Phoenix", "America/Sao_Paulo", "America/Toronto", "America/Vancouver", "Asia/Dubai", "Asia/Hong_Kong", "Asia/Kolkata", "Asia/Seoul", "Asia/Shanghai",
        "Asia/Singapore", "Asia/Tokyo", "Australia/Melbourne", "Australia/Sydney", "Europe/Amsterdam", "Europe/Berlin", "Europe/Dublin", "Europe/Helsinki",
        "Europe/Lisbon", "Europe/London", "Europe/Madrid", "Europe/Paris", "Europe/Stockholm", "Europe/Warsaw", "Europe/Zurich", "Pacific/Auckland", "Pacific/Honolulu",
    ];

    /// <inheritdoc />
    public string Type => "schedule";

    /// <inheritdoc />
    public string Title => "Schedule";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Triggers;

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("cron", BlockFieldType.Text, Caption: "Runs at", Presets: [.. _presets.Select(static p => new BlockChoice(p.Cron, p.Label))]),
        new("timezone", BlockFieldType.Text, Required: false, Caption: "Time zone", Presets: [.. _zones.Select(static z => new BlockChoice(z, z))]),
    ]);

    /// <inheritdoc />
    public string Example => """{"cron":"0 7 * * 1-5","timezone":"America/Los_Angeles"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <summary>"Runs every weekday at 7:00, America/Los_Angeles" for a preset cron, "Runs every Thursday at 7:00, UTC" for another daily or weekly one, "Runs on schedule &lt;cron&gt;, &lt;zone&gt;" for any other, the title and "No time set yet" with no cron. No zone is UTC.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The line.</returns>
    public BlockSummary Summarize(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var cron = Text(config["cron"]);
        if (string.IsNullOrWhiteSpace(cron))
        {
            return new BlockSummary(Title, "No time set yet");
        }

        var zone = Text(config["timezone"]) is { Length: > 0 } z ? z : "UTC";
        var preset = Array.FindIndex(_presets, p => string.Equals(p.Cron, cron.Trim(), StringComparison.Ordinal));
        var runs = preset >= 0 ? _presets[preset].Runs : ScheduleWords.Of(cron);
        return new BlockSummary(runs is not null ? $"Runs {runs}, {zone}" : $"Runs on schedule {cron.Trim()}, {zone}");
    }

    /// <summary>The same line worded in <paramref name="local"/> (the zone the list and the next run use): "Runs Sun–Thu at 23:00" for 7:00 UTC weekdays in Pacific winter time. A cron of another shape, a zone this box cannot read, or two zones that agree keep <see cref="Summarize(JsonObject)"/>.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="local">The zone to word the time in.</param>
    /// <param name="now">When the offsets are read.</param>
    /// <returns>The line.</returns>
    public BlockSummary Summarize(JsonObject config, TimeZoneInfo local, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(local);
        return Text(config["cron"]) is { } cron && ScheduleWords.Local(cron, Text(config["timezone"]), local, now) is { } words ? new BlockSummary($"Runs {words}") : Summarize(config);
    }

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["scheduledFor"] = context.Resolve("trigger.scheduledFor"),
            ["localDate"] = context.Resolve("trigger.localDate"),
            ["weekday"] = context.Resolve("trigger.weekday"),
        }));
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
