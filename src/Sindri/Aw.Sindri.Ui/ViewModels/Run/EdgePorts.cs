using System.Runtime.InteropServices;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The sides an edge leaves and arrives on.</summary>
/// <param name="From">The side of the source card.</param>
/// <param name="To">The side of the target card.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EdgePorts(PortSide From, PortSide To);
