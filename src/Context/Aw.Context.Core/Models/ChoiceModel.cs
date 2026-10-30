namespace Aw.Context.Core.Models;

/// <summary>
/// The model behind an <see cref="IChoicePort"/>, as one string: the Jev model id plus its version (spec §6.7). It is a property of the port, not of each answer,
/// because Context's answer cache needs it before a question is asked; a changed one is a deliberate miss for every cached answer.
/// </summary>
/// <param name="Value">The id and version, for example 'jev-1.13.0'; 'unknown' when the provider reports none.</param>
public sealed record ChoiceModel(string Value)
{
    /// <summary>What the port says when its provider reports no model, which makes every cache key miss.</summary>
    public static ChoiceModel Unknown { get; } = new("unknown");

    /// <inheritdoc/>
    public override string ToString() => Value;
}
