using Aw.Context.Core.Concepts;
using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>The two model passes of a map build, one collaborator: the cluster labeler and the concept namer.</summary>
/// <param name="Labeler">Names the clusters.</param>
/// <param name="Concepts">Names the derived concepts.</param>
internal sealed record MapNamers(ILabeler Labeler, IConceptNamer Concepts);
