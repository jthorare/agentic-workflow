namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One of the words people use on the Pin sheet.</summary>
/// <param name="Word">The word.</param>
/// <param name="NewRecords">How many stored records the alias rule would newly link to the pin; null while counting.</param>
/// <param name="Ticked">Whether it goes into the pin's aliases.</param>
internal sealed record PinWordRow(string Word, int? NewRecords, bool Ticked);
