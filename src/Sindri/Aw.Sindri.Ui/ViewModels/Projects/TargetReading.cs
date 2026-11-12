using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The answer of <see cref="TargetDateReader.Read"/>: what the text was, and the date when it was a date.</summary>
/// <param name="Kind">What the text was.</param>
/// <param name="Date">The date read; the default date unless <paramref name="Kind"/> is <see cref="TargetReadKind.Date"/>.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TargetReading(TargetReadKind Kind, DateOnly Date);
