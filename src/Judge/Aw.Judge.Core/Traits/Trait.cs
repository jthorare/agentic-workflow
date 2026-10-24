namespace Aw.Judge.Core.Traits;

/// <summary>One reusable code rule, read from a '&lt;name&gt;.yaml' trait file.</summary>
/// <param name="Name">The rule's name; it matches the file's name.</param>
/// <param name="Subject">What the rule is about.</param>
/// <param name="Requirement">What the code must do; authoritative.</param>
/// <param name="Purpose">Why the rule exists.</param>
/// <param name="Severity">Whether the Stop gate holds the agent back over it.</param>
/// <param name="File">The trait file.</param>
/// <param name="Applicability">When the rule applies, or null.</param>
/// <param name="Guidance">How to fix a breach, or null.</param>
/// <param name="Context">Examples, or null.</param>
/// <param name="Paths">Repo-relative globs that scope the rule to files without a model call, or null for every file.</param>
public sealed record Trait(
    string Name,
    string Subject,
    string Requirement,
    string Purpose,
    TraitSeverity Severity,
    string File,
    string? Applicability = null,
    string? Guidance = null,
    string? Context = null,
    IReadOnlyList<string>? Paths = null);
