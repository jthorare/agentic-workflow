using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The window a recipe's stats cover (API spec §3.8, E-24): 7, 30 (the default) or 90 days back from the read.</summary>
public sealed record StatsWindow
{
    private StatsWindow(string text, TimeSpan span)
    {
        Text = text;
        Span = span;
    }

    /// <summary>The seven-day window.</summary>
    public static StatsWindow SevenDays { get; } = new("7d", TimeSpan.FromDays(7));

    /// <summary>The thirty-day window, the default.</summary>
    public static StatsWindow ThirtyDays { get; } = new("30d", TimeSpan.FromDays(30));

    /// <summary>The ninety-day window.</summary>
    public static StatsWindow NinetyDays { get; } = new("90d", TimeSpan.FromDays(90));

    /// <summary>The window as the API spells it: '7d', '30d' or '90d'.</summary>
    public string Text { get; }

    /// <summary>How far back the window reaches.</summary>
    public TimeSpan Span { get; }

    /// <summary>The window <paramref name="text"/> names; none given is the default, anything but the three is SND-API-013.</summary>
    /// <param name="text">The 'window' query value, or null.</param>
    /// <returns>The window.</returns>
    public static Result<StatsWindow> Parse(string? text) => text switch
    {
        null => ThirtyDays,
        "7d" => SevenDays,
        "30d" => ThirtyDays,
        "90d" => NinetyDays,
        _ => SindriErrors.InvalidQuery.With("A window is 7d, 30d or 90d (fix: pass window=7d, window=30d or window=90d)"),
    };
}
