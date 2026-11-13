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
        ("0 8 * * 1", "Mondays at 8:00", "on Mondays at 8:00"),
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

    /// <summary>"Runs every weekday at 7:00, America/Los_Angeles" for a preset cron, "Runs on schedule &lt;cron&gt;, &lt;zone&gt;" for any other, the title and "No time set yet" with no cron. No zone is UTC.</summary>
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
        return new BlockSummary(preset >= 0 ? $"Runs {_presets[preset].Runs}, {zone}" : $"Runs on schedule {cron.Trim()}, {zone}");
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
