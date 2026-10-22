using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Code;

/// <summary>What <see cref="SymbolColumn"/> reads.</summary>
/// <param name="Repo">The repo the files are in.</param>
/// <param name="Version">The map version read.</param>
/// <param name="Paths">The files to read; at most <see cref="CodeRules.MaxSymbolFiles"/> are asked for.</param>
/// <param name="Links">Each file with the nodes it implements.</param>
public sealed record SymbolColumnReading(ContextRepo Repo, MapVersion Version, IReadOnlyList<string> Paths, IReadOnlyList<(string Path, IReadOnlyList<NodeKey> Nodes)> Links);
