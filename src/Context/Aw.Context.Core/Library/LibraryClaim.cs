using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>One <c language="text">Covers:</c> ref of a PR, with what it resolves to now (claims resolve on read, spec 2026-11-04 §4.4).</summary>
/// <param name="Pr">The PR record.</param>
/// <param name="PrState">The PR's state; a closed one was not merged.</param>
/// <param name="Resolution">What the ref names today.</param>
public sealed record LibraryClaim(RecordKey Pr, RecordState? PrState, LibraryRefResolution Resolution);
