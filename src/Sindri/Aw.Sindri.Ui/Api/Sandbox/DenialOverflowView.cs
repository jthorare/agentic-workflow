namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>The denials of one kind not counted by target.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Count">How many.</param>
internal sealed record DenialOverflowView(string Kind, int Count);
