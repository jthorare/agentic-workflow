namespace Aw.Redaction;

/// <summary>
/// A scrub pattern from configuration (a profile's 'context.scrub.extraPatterns' entry): an identifier shape the
/// built-in rules don't know, such as a customer id.
/// <see cref="Redactor.Create(IReadOnlyList{ExtraPattern}, TimeSpan, TimeProvider)"/> validates it against Redaction's pattern rules and compiles it with the redactor's match timeout.
/// </summary>
/// <param name="Id">The rule id the placeholder names ('[REDACTED:acme-customer-id]'): lowercase letters, digits
/// and single hyphens, starting with a letter, at most 64 characters, not a built-in rule id, 'blocked' or
/// 'secret-field', and not repeated in the list.</param>
/// <param name="Pattern">The .NET regex, written with explicit ASCII classes ('ACME-[0-9]{6}', never
/// '\d'). When it has a group named 'v', only that group is redacted.</param>
public sealed record ExtraPattern(string Id, string Pattern);
