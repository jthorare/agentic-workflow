using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.BuildMap;

/// <summary>A directory doc and the home feature at its directory (spec 2026-10-27 §11.2).</summary>
/// <param name="Doc">The doc's record node key.</param>
/// <param name="Evidence">The doc's record key, which the edge cites.</param>
/// <param name="Feature">The home feature at the doc's directory.</param>
internal sealed record DirectoryDoc(NodeKey Doc, RecordKey Evidence, NodeKey Feature);
