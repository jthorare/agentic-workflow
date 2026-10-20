namespace Aw.Sindri.Features.ResolveParents;

/// <summary>Check that every parent of a line packet is a frozen packet (LN-PKT6), before the reader reads the packet.</summary>
/// <param name="Parents">The hashes of the packet's index.json 'parents' ('sha256:' and 64 lower-case hex digits).</param>
public sealed record ResolveParentsRequest(IReadOnlyList<string> Parents);
