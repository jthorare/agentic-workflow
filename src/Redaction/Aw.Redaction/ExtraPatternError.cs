namespace Aw.Redaction;

/// <summary>Why one configured extra pattern was refused. A caller reports it with its own key path, for example
/// 'context.scrub.extraPatterns[2].pattern' for <see cref="Index"/> 2 and
/// <see cref="ExtraPatternField.Pattern"/>.</summary>
/// <param name="Index">The position of the refused entry in the list passed to
/// <see cref="Redactor.Create(IReadOnlyList{ExtraPattern}, TimeSpan, TimeProvider)"/>.</param>
/// <param name="Field">Which field of the entry is wrong.</param>
/// <param name="Code"><see cref="RedactionCodes.InvalidPatternId"/> or
/// <see cref="RedactionCodes.InvalidPattern"/>.</param>
/// <param name="Message">What is wrong and how to fix it, for people. It never quotes the pattern.</param>
public sealed record ExtraPatternError(int Index, ExtraPatternField Field, string Code, string Message);
