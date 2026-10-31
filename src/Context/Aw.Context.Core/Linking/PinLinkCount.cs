using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Linking;

/// <summary>The distinct records linked to one pin's concept node by any rule.</summary>
/// <param name="Id">The pin id (its node is concept:&lt;id&gt;).</param>
/// <param name="Before">Records linked before the run.</param>
/// <param name="After">Records linked after it.</param>
public sealed record PinLinkCount(ConceptId Id, int Before, int After);
