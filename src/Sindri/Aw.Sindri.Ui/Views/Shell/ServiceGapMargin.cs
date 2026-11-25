using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>
/// The Services card's last-run margin from the card's row width (#4027): 8 px on the left where the card has room, so last run starts
/// Space.SM (12) after the widest cadence with the column gap; 4 px under <see cref="RoomFrom"/>, so the gap falls back to Space.XS (8)
/// and the 20-character names stay whole. 8 px above and below, as every System card row's text.
/// </summary>
#pragma warning disable MA0182 // used from XAML (HealthPage's Services rows)
internal sealed class ServiceGapMargin : IValueConverter
{
    /// <summary>
    /// The row width from which the 12 px gap fits: at text scale 1.3 with the widest clock the row needs about 426 px for the longest
    /// name; the row is 422 px at a 1000 px window and 562 px at 1280, so 480 gives 8 px at 1000 and 12 px at 1280 at every text scale.
    /// </summary>
    internal const double RoomFrom = 480;

    /// <summary>Gets the one converter.</summary>
    public static ServiceGapMargin Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(value is double width && width >= RoomFrom ? 8 : 4, 8, 0, 8);

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
#pragma warning restore MA0182
