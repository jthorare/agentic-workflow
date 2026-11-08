using System.Globalization;
using System.Text.Json;

namespace Aw.Meta.Core.Routing;

/// <summary>Reads Codex's usage windows from the end of a rollout file.</summary>
public static class CodexRateLimits
{
    private const string Marker = "\"rate_limits\"";
    private static readonly string[] _windowNames = ["primary", "secondary"];

    /// <summary>Finds the last rate_limits snapshot.</summary>
    /// <param name="lines">The rollout's last lines, oldest first.</param>
    /// <param name="now">The current time; a window whose reset time has passed counts as unused.</param>
    /// <returns>The signal, or null when no line carries one.</returns>
    public static ProviderSignal? Read(IReadOnlyList<string> lines, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Contains(Marker, StringComparison.Ordinal) && Snapshot(lines[i], now) is { } signal)
            {
                return signal;
            }
        }

        return null;
    }

    private static ProviderSignal? Snapshot(string line, DateTimeOffset now)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            if (Child(Child(document.RootElement, "payload"), "rate_limits") is not { } limits)
            {
                return null;
            }

            var windows = _windowNames.Select(name => Window(Child(limits, name), now)).OfType<ProviderWindow>().ToList();

            // A reached type left over from a window that has since reset says nothing about now.
            var reached = limits.TryGetProperty("rate_limit_reached_type", out var type) && type.ValueKind != JsonValueKind.Null
                && (windows.Count == 0 || windows.Exists(w => w.ResetsAt is null || w.ResetsAt > now));
            return new ProviderSignal(windows, reached || windows.Exists(static w => w.UsedPct >= 100));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Child(JsonElement? parent, string name) =>
        parent is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.Object ? child : null;

    private static ProviderWindow? Window(JsonElement? window, DateTimeOffset now)
    {
        if (window is not { } element || Number(element, "used_percent") is not { } used)
        {
            return null;
        }

        DateTimeOffset? resets = Number(element, "resets_at") is { } seconds ? DateTimeOffset.FromUnixTimeSeconds((long)seconds) : null;
        var expired = resets is { } at && at <= now;
        return new ProviderWindow(Name((int)(Number(element, "window_minutes") ?? 0)), expired ? 0 : used, resets);
    }

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static string Name(int minutes) =>
        minutes switch
        {
            300 => "5h",
            10080 => "weekly",
            > 0 when minutes % 60 == 0 => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}h"),
            > 0 => string.Create(CultureInfo.InvariantCulture, $"{minutes}m"),
            _ => "window",
        };
}
