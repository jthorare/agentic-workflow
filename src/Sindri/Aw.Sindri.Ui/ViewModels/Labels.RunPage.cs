using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the run page (F3 spec 3 §6, slice 3). Never a served sentence.</summary>
internal static partial class Labels
{

    /// <summary>"Plan stage, step 2 of 5".</summary>
    /// <param name="stage">The stage's name.</param>
    /// <param name="i">The step's place.</param>
    /// <param name="n">How many steps.</param>
    /// <returns>The words.</returns>
    internal static string StepOf(string stage, int i, int n) => string.Create(CultureInfo.InvariantCulture, $"{stage} stage, step {i} of {n}");

}
