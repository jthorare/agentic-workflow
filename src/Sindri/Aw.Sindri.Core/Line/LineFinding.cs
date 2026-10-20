using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One finding, in #258's shape (scoping spec §8.2) for every line station.</summary>
/// <param name="Rule">The rule id, for example 'DSN-COV-001'.</param>
/// <param name="Item">What it is about (an id, a key, a lens), or null for the whole packet.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to change.</param>
/// <param name="Severity">Fail or warn.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LineFinding(string Rule, string? Item, string Message, string Fix, LineSeverity Severity);
