namespace Aw.Sindri.Core.Setup;

/// <summary>Why a step failed: the catalogue code (for a bug report) and one plain sentence (for the person).</summary>
/// <param name="Code">The error code, such as SND-LEDGER-002.</param>
/// <param name="Sentence">What went wrong in plain words, with no code or field name in it.</param>
public sealed record SetupError(string Code, string Sentence);
