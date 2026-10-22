namespace Aw.Sindri.Features.Line;

/// <summary>The dispatcher's decision (line spec §8.2).</summary>
/// <param name="Outcome"><c language="text">start</c>, <c language="text">full</c>, <c language="text">done</c> or <c language="text">blocked</c>.</param>
/// <param name="Next">The phase to start, with <c language="text">start</c>.</param>
internal sealed record PhaseDecision(string Outcome, NextPhase? Next);
