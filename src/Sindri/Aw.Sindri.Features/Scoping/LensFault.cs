namespace Aw.Sindri.Features.Scoping;

/// <summary>One way a lens's entry disagrees with itself or with its coverage row.</summary>
/// <param name="Message">What is wrong, counted: one fault per kind, never one per edge.</param>
/// <param name="Fix">What to change.</param>
internal readonly record struct LensFault(string Message, string Fix);
