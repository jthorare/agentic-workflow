namespace Aw.Sindri.Core.Packets;

/// <summary>What 'aw-packets validate' found.</summary>
/// <param name="Passed">True when no check failed (an unchecked rule does not fail).</param>
/// <param name="Failures">The failed checks, in the tool's order; empty when it passed.</param>
public sealed record PacketValidation(bool Passed, IReadOnlyList<PacketFailure> Failures);
