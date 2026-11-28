using System.Globalization;

namespace Aw.Sindri.Core.Health;

/// <summary>How long ago something happened, in words.</summary>
internal static class AgeWords
{
    /// <summary>"under 1 min", "15 min", "5 h" or "3 d": how long something has lasted, for "red for 3 d" (#4333).</summary>
    /// <param name="age">How long it has lasted; a negative one (clock skew) reads as under a minute.</param>
    /// <returns>The words.</returns>
    internal static string For(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "under 1 min",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d"),
    };

    /// <summary>"just now", "1 min ago", "3 h ago" or "2 d ago".</summary>
    /// <param name="age">How long ago it was.</param>
    /// <returns>The words.</returns>
    internal static string Ago(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min ago"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d ago"),
    };
}
