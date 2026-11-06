using Aw.Sindri.Core.Skills;

namespace Aw.Sindri.Testing.Skills;

/// <summary>The fixture skill blocks (skill flows F-1): the folder of two fixture manifests, and the same entries as records for the tests that need no file.</summary>
public static class SkillFixtures
{
    /// <summary>The skills folder with 'review' and 'officeHours' (two blocks), copied to the test output.</summary>
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "skills");

    /// <summary>The 'review' entry of the fixture folder.</summary>
    public static SkillBlockManifest Review { get; } = new(
        "review", "Reviews a pull request with domain reviewers and says pass or needs changes.",
        [new SkillInput("pr", SkillValueType.Text, "Pull request (number or link)", Required: true)],
        [new SkillArg("{pr}")],
        [new SkillOutput("findings", SkillValueType.Path, "review_file")],
        new SkillOutcomes("verdict", new Dictionary<string, string>(StringComparer.Ordinal) { ["PASS"] = "pass", ["NEEDS_CHANGES"] = "needsChanges" }, "pass"),
        Pushes: false, TimeSpan.FromHours(2));

    /// <summary>A block with an optional text input behind a flag, and a report taken from one artifact by file name (skill flows spec §3.1's autoplan).</summary>
    public static SkillBlockManifest Autoplan { get; } = new(
        "autoplan", "Reviews a plan through five lenses and says pass, needs work or blocked.",
        [
            new SkillInput("plan", SkillValueType.Path, "Plan folder", Required: true),
            new SkillInput("mode", SkillValueType.Text, "Review mode", Required: false, ["mvp", "growth", "scale", "pivot"]),
        ],
        [new SkillArg("{plan}"), new SkillArg(Text: null, Flag: "--mode", Input: "mode")],
        [new SkillOutput("report", SkillValueType.Path, "artifacts[consolidated-review.md]"), new SkillOutput("featureDir", SkillValueType.Path, "feature_dir")],
        new SkillOutcomes("verdict", new Dictionary<string, string>(StringComparer.Ordinal) { ["PASS"] = "pass", ["NEEDS_WORK"] = "needsWork", ["BLOCKED"] = "blocked" }, "pass"),
        Pushes: false, TimeSpan.FromHours(3));

    /// <summary>The 'office-hours' entry of the fixture folder.</summary>
    public static SkillBlockManifest OfficeHours { get; } = new(
        "office-hours", "Shapes an idea into a plan folder with requirements and tasks.",
        [new SkillInput("idea", SkillValueType.Text, "What do you want built?", Required: true)],
        [new SkillArg("{idea}")],
        [new SkillOutput("featureDir", SkillValueType.Path, "feature_dir")],
        new SkillOutcomes(From: null, new Dictionary<string, string>(StringComparer.Ordinal), "done"),
        Pushes: false, TimeSpan.FromHours(3));

    /// <summary>The 'bug-fix-orchestrator' entry (skill flows spec §3.1), which pushes.</summary>
    public static SkillBlockManifest BugFixOrchestrator { get; } = new(
        "bug-fix-orchestrator", "Reproduces a bug with a failing check, fixes it until that check passes, and opens a draft PR.",
        [new SkillInput("bug", SkillValueType.Text, "The bug (a Linear id, a link or the report)", Required: true)],
        [new SkillArg("{bug}")],
        [new SkillOutput("pr", SkillValueType.Text, "pr"), new SkillOutput("branch", SkillValueType.Text, "branch"), new SkillOutput("resolution", SkillValueType.Path, "artifacts[resolution.md]")],
        new SkillOutcomes("status", new Dictionary<string, string>(StringComparer.Ordinal) { ["resolved"] = "resolved", ["partial"] = "partial", ["unresolved"] = "unresolved" }, "resolved"),
        Pushes: true, TimeSpan.FromHours(8));
}
