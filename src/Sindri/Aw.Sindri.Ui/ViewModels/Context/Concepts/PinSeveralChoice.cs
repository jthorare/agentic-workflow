namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>One choice of an Add several row's match picker: a derived concept of the map, or "New, no match yet".</summary>
/// <param name="Key">The concept's key, or null for "New, no match yet".</param>
/// <param name="Name">What the picker shows.</param>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts the Add several view model that holds them
internal sealed record PinSeveralChoice(string? Key, string Name)
{
    /// <summary>The choice of a line no concept fits.</summary>
    internal static PinSeveralChoice New { get; } = new(Key: null, Labels.PinSeveralNoMatch);

    /// <inheritdoc />
    public override string ToString() => Name;
}
#pragma warning restore MA0182
