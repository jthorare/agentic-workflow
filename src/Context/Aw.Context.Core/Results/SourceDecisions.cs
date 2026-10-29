using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>What the decision pass did for one source.</summary>
/// <param name="Source">The source.</param>
/// <param name="Looked">The stored records read for decisions in this pass (a record already read at its stored version is skipped).</param>
/// <param name="Decisions">The decisions the intent layer holds for the source after the pass.</param>
/// <param name="Code">The error code the pass ended with, or null.</param>
public sealed record SourceDecisions(SourceId Source, int Looked, int Decisions, string? Code);
