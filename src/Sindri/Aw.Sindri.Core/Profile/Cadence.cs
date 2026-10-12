using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>How often a service runs, parsed from the profile's 'every' text.</summary>
/// <param name="Interval">The interval.</param>
public sealed record Cadence(TimeSpan Interval)
{
    /// <summary>The shortest cadence.</summary>
    public static readonly TimeSpan Min = TimeSpan.FromSeconds(5);

    /// <summary>The longest cadence.</summary>
    public static readonly TimeSpan Max = TimeSpan.FromHours(24);

    /// <summary>Parses '30s', '5m' or '2h'.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The cadence, or SND-PROFILE-002 explaining the expected form.</returns>
    public static Result<Cadence> Parse(string text)
    {
        if (text.Length < 2 || !long.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return Invalid(text);
        }

        TimeSpan? interval = text[^1] switch
        {
            's' => TimeSpan.FromSeconds(Math.Min(n, 1_000_000)),
            'm' => TimeSpan.FromMinutes(Math.Min(n, 100_000)),
            'h' => TimeSpan.FromHours(Math.Min(n, 10_000)),
            _ => null,
        };
        return interval is { } value && value >= Min && value <= Max ? new Cadence(value) : Invalid(text);
    }

    /// <summary>The cadence as profile text, in the largest whole unit.</summary>
    public override string ToString() => Interval switch
    {
        { TotalHours: var h } when h >= 1 && h == Math.Floor(h) => string.Create(CultureInfo.InvariantCulture, $"{h}h"),
        { TotalMinutes: var m } when m >= 1 && m == Math.Floor(m) => string.Create(CultureInfo.InvariantCulture, $"{m}m"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{Interval.TotalSeconds}s"),
    };

    private static Error Invalid(string text) => SindriErrors.ProfileInvalid.With(
        $"'every: {text}' is not a cadence: expected a whole number and s, m or h (for example 30s), from 5s to 24h");
}
