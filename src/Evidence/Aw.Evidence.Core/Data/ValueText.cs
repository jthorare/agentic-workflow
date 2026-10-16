using System.Globalization;

namespace Aw.Evidence.Core.Data;

/// <summary>How a date or time value is written in recorded rows, in the invariant culture. The run learns a secret value in the same form, so it can hide it wherever it is recorded.</summary>
public static class ValueText
{
    /// <summary>A date as yyyy-MM-dd.</summary>
    /// <param name="day">The date.</param>
    /// <returns>The text.</returns>
    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A time of day as HH:mm:ss, with the seven fraction digits only when there is a fraction.</summary>
    /// <param name="time">The time.</param>
    /// <returns>The text.</returns>
    public static string Time(TimeOnly time) => time.ToString(time.Ticks % TimeSpan.TicksPerSecond == 0 ? "HH:mm:ss" : "HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

    /// <summary>A moment in the round-trip format.</summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The text.</returns>
    public static string Moment(DateTime moment) => moment.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>A moment with an offset in the round-trip format.</summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The text.</returns>
    public static string Moment(DateTimeOffset moment) => moment.ToString("O", CultureInfo.InvariantCulture);
}
