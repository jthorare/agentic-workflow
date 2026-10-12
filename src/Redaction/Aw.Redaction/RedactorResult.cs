using System.Diagnostics.CodeAnalysis;

namespace Aw.Redaction;

/// <summary>The result of <see cref="Redactor.Create(IReadOnlyList{ExtraPattern}, TimeSpan, TimeProvider)"/>: a
/// redactor over the built-in rules and the extra patterns, or every reason the extra patterns were refused. Redaction
/// sits beside Aw.Kernel at tier 0 and can't use its Result; a caller maps the errors to its own.</summary>
public sealed class RedactorResult
{
    private RedactorResult(Redactor? redactor, IReadOnlyList<ExtraPatternError> errors)
    {
        Redactor = redactor;
        Errors = errors;
    }

    /// <summary>The redactor; null when <see cref="IsValid"/> is false.</summary>
    public Redactor? Redactor { get; }

    /// <summary>Every refused field, in entry order (an entry's id before its pattern); empty when valid.</summary>
    public IReadOnlyList<ExtraPatternError> Errors { get; }

    /// <summary>True when every extra pattern was accepted and <see cref="Redactor"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Redactor))]
    public bool IsValid => Redactor is not null;

    internal static RedactorResult Valid(Redactor redactor) => new(redactor, []);

    internal static RedactorResult Invalid(IReadOnlyList<ExtraPatternError> errors) => new(redactor: null, errors);
}
