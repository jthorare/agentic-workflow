using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>The denials of one kind not counted by target.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Count">How many.</param>
public sealed record DenialOverflow(DenialKind Kind, int Count);
