using Aw.Kernel;

namespace Aw.Sindri.Core.Line;

/// <summary>
/// The line's error catalogue (line spec Appendix D, LN-API8), beside <see cref="SindriErrors"/> and validated with it. The SND-PACK-* codes are the pack lane's (LN-PK) and
/// are not registered here.
/// </summary>
public static class LineErrors
{
    /// <summary>SND-LINE-001: a packet's hash doesn't match its freeze record, a candidate its hash, or a parent in its index.json resolves to no frozen packet (LN-PKT6).</summary>
    public static readonly ErrorDefinition PacketMismatch = new("SND-LINE-001", ErrorKind.Conflict, "A packet's hash does not match its freeze record");

    /// <summary>SND-LINE-002: a phase start whose packet a live instance holds, or whose dependencies are not merged or shipped.</summary>
    public static readonly ErrorDefinition PhaseBlocked = new("SND-LINE-002", ErrorKind.Conflict, "A phase cannot start: a live instance holds its packet or its dependencies are not merged");

    /// <summary>SND-LINE-003: a line view asked of the wrong recipe's instance.</summary>
    public static readonly ErrorDefinition WrongRecipe = new("SND-LINE-003", ErrorKind.Invalid, "The instance runs another recipe than this view reads");

    /// <summary>SND-LINE-004: 'line doctor' (or 'seed') found missing setup.</summary>
    public static readonly ErrorDefinition SetupMissing = new("SND-LINE-004", ErrorKind.Invalid, "The line's setup is missing");

    /// <summary>The catalogue's codes.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [PacketMismatch, PhaseBlocked, WrongRecipe, SetupMissing];
}
