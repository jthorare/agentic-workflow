namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>Turns the owner's pasted list into concepts (concept pins spec §3.6 "Add several"). Red: the tests name the rules first.</summary>
#pragma warning disable MA0182 // Q7b: the Add several view model and its tests use it
internal static class PinListParser
{
    /// <summary>The most concepts one list takes; the rest is left out.</summary>
    internal const int MaxLines = 100;

    /// <summary>The longest name a pin has.</summary>
    internal const int MaxName = 80;

    /// <summary>Reads a pasted list.</summary>
    /// <param name="text">The box's text.</param>
    /// <returns>The concepts.</returns>
    internal static PinListRead Read(string? text) => throw new NotSupportedException();
}
#pragma warning restore MA0182
