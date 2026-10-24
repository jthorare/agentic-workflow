using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>One edge of a callers or callees answer: the facade's edge, coverage included.</summary>
/// <param name="Level">1 to 8: how far from the asked symbol.</param>
/// <param name="Other">The symbol at the other end, or null when it is not in this repo or not resolved.</param>
/// <param name="Name">The called name.</param>
/// <param name="Kind">Calls or inherits.</param>
/// <param name="Basis">Syntax or graphify; read it beside a 'likely' confidence.</param>
/// <param name="Confidence">How sure.</param>
/// <param name="Coverage">Full for C#, partial for a graphify language, unavailable for an unmeasured one.</param>
/// <param name="Line">The line in the caller.</param>
/// <param name="Candidates">How many symbols the name matched.</param>
public sealed record CodeCallRow(int Level, SymbolHit? Other, string Name, EdgeKind Kind, EdgeBasis Basis, Confidence Confidence, Coverage Coverage, int Line, int Candidates)
{
    /// <summary>The row for a facade edge.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The row.</returns>
    internal static CodeCallRow From(CallEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return new(edge.Level, edge.Other, edge.Name, edge.Kind, edge.Basis, edge.Confidence, edge.Coverage, edge.Line, edge.Candidates);
    }
}
