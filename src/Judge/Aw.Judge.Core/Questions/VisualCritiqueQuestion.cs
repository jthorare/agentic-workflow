using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Questions;

/// <summary>
/// visual-critique: does a screenshot look right ('looks-right'), have specific problems ('looks-off') or look careless
/// ('sloppy'), with 'reasons'? No rule: whether it looks right is exactly the judgment this question exists for. The
/// input check wants an absolute evidence directory and screenshot paths relative to it that stay inside it.
/// </summary>
public static class VisualCritiqueQuestion
{
    /// <summary>The question's name.</summary>
    public const string Name = "visual-critique";

    /// <summary>It looks right.</summary>
    public const string LooksRight = "looks-right";

    /// <summary>It has specific visual problems.</summary>
    public const string LooksOff = "looks-off";

    /// <summary>It looks careless throughout.</summary>
    public const string Sloppy = "sloppy";

    /// <summary>The definition.</summary>
    public static QuestionDefinition<VisualCritiqueInput> Definition { get; } = new()
    {
        Name = Name,
        Outputs = [LooksRight, LooksOff, Sloppy],
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LooksRight] = "alignment, spacing, hierarchy and weight look right and match the reference",
            [LooksOff] = "there are specific visual problems compared with the reference or expectations",
            [Sloppy] = "the page looks careless, with pervasive alignment, spacing or hierarchy problems",
        },
        ContentClass = ContentClass.Image,
        Threshold = 0.5,
        TimeBudget = TimeSpan.FromSeconds(20),
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [ResolutionCheckQuestion.Reasons] = ResolutionCheckQuestion.StringList() },
        Prompt = Prompt,
        InputProblem = InputProblem,
    };

    // The evidence directory is the CLIs' working directory and their Read scope: it must be absolute, and each
    // screenshot a relative path that stays inside it. None may hold a control character or U+2028/U+2029 (a line
    // break would let a path start a line of its own in the prompt, and cursor names the full paths in it).
    internal static string? InputProblem(VisualCritiqueInput input) =>
        !IsAbsolute(input.EvidenceDir) || HasLineBreakOrControl(input.EvidenceDir) ? "evidenceDir must be an absolute path with no control character"
        : !StaysInside(input.AfterScreenshot) ? "afterScreenshot must be a relative path inside evidenceDir"
        : input.BaselineScreenshot is { } baseline && !StaysInside(baseline) ? "baselineScreenshot must be a relative path inside evidenceDir"
        : null;

    private static bool IsAbsolute(string path) =>
        path.StartsWith('/') || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\');

    private static bool HasLineBreakOrControl(string path) => path.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029');

    private static bool StaysInside(string path) =>
        path.Length > 0
        && !HasLineBreakOrControl(path)
        && !path.StartsWith('/') && !path.StartsWith('\\') && !(path.Length >= 2 && path[1] == ':')
        && !path.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    // Provider-neutral: a CLI provider reads the file from the evidence directory or attaches it as an image.
    private static string Prompt(VisualCritiqueInput input) => string.Join('\n',
    [
        $"Look at the screenshot {UntrustedText.OneLine(input.AfterScreenshot)}.",
        input.BaselineScreenshot is { } baseline
            ? $"Then look at {UntrustedText.OneLine(baseline)}, a screenshot of the same page on the base branch before this change, and compare them."
            : "There is no base-branch screenshot to compare against: judge it on its own.",
        "Rate alignment, spacing, typographic hierarchy, visual weight, and consistency with the reference (when given).",
        "Reply {\"decision\":\"looks-right\",\"reasons\":[]} if it looks right, or {\"decision\":\"looks-off\",\"reasons\":[\"...\"]} or {\"decision\":\"sloppy\",\"reasons\":[\"...\"]} listing the specific problems. Never guess \"looks-right\" if anything looks wrong.",
    ]);
}
