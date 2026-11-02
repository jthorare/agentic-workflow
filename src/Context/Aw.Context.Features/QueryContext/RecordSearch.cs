using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Context.Core.Vectors;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the query reads the record store with (spec §6.1): its lexical leg, the store itself, and the dense leg's three ports.</summary>
/// <param name="Leg">The lexical leg.</param>
/// <param name="Store">The record store the legs' hits are read back from.</param>
/// <param name="Embedder">Embeds the query for the dense leg.</param>
/// <param name="Matrix">The in-memory chunk vectors the dense leg scans.</param>
/// <param name="Scan">The nearest-rows scan.</param>
internal sealed record RecordSearch(ILexicalLeg Leg, IRecordStore Store, IQueryEmbedder Embedder, IVectorMatrixCache Matrix, IVectorScan Scan);
