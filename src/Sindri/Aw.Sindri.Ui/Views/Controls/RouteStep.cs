using System.Runtime.InteropServices;
using Avalonia;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>One step of an <see cref="EdgeRoute"/>.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="To">Where the step ends.</param>
/// <param name="Radius">The arc's radius (arcs only).</param>
/// <param name="Clockwise">Whether the arc turns clockwise on the screen (arcs only).</param>
/// <param name="Control1">The first control point (cubics only).</param>
/// <param name="Control2">The second control point (cubics only).</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RouteStep(RouteStepKind Kind, Point To, double Radius = 0, bool Clockwise = false, Point Control1 = default, Point Control2 = default);
