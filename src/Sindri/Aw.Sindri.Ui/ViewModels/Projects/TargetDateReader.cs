using Aw.Kernel.Dates;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>Reads the Add project sheet's Target box: blank is no target, anything else goes to <see cref="DueWords.Resolve"/>, the one due-word parser. Pure: today comes in as an argument.</summary>
internal static class TargetDateReader
{
    /// <summary>Reads the text.</summary>
    /// <param name="text">What is in the box.</param>
    /// <param name="today">Today's date, from the caller's clock.</param>
    /// <returns>Empty for blank text, the date for a readable one, Unreadable otherwise.</returns>
    internal static TargetReading Read(string? text, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TargetReading(TargetReadKind.Empty, default);
        }

        var read = DueWords.Resolve(text, today);
        return read.IsSuccess ? new TargetReading(TargetReadKind.Date, read.Value) : new TargetReading(TargetReadKind.Unreadable, default);
    }
}
