using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Where the run canvas's world sits on its stage: a scale and the screen offset of the world's origin. The view turns it into the stage's own view.</summary>
/// <param name="Scale">World to screen scale.</param>
/// <param name="X">Screen x of the world's origin.</param>
/// <param name="Y">Screen y of the world's origin.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RunFocus(double Scale, double X, double Y);
