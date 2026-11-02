using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Pages;

/// <summary>The suggested page a pasted block names.</summary>
/// <param name="Key">The surface's key.</param>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name on the map.</param>
/// <param name="Actions">How many actions it has.</param>
public sealed record PageListMatch(NodeKey Key, PageId Id, string Name, int Actions);
