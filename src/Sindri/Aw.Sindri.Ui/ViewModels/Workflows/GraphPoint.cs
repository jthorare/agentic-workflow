using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A point in the graph's world, in pixels.</summary>
/// <param name="X">Horizontal.</param>
/// <param name="Y">Vertical.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GraphPoint(double X, double Y);
