using System.Globalization;

namespace Aw.Sindri.Core.Health;

/// <summary>How long ago something happened, in words.</summary>
internal static class AgeWords
{
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
