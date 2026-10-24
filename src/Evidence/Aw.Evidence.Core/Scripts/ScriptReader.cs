using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Evidence.Core.Scripts;

/// <summary>Reads a UI script document and checks its rules.</summary>
public static partial class ScriptReader
{
    /// <summary>Parses and validates a script.</summary>
    /// <param name="utf8Json">The script file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The script, or AW-EVIDENCE-002 naming every problem.</returns>
    public static Result<UiScript> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<UiScript>(utf8Json, options, EvidenceErrors.ScriptInvalid).Bind(Validate);

    /// <summary>Checks a script's rules: a kebab-case role, at least one step and viewport, known enum values, well-formed expectations, non-negative usage.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The script, or AW-EVIDENCE-002 naming every problem.</returns>
    public static Result<UiScript> Validate(UiScript script)
    {
        var problems = new Problems();
        problems.AddIf(script.Role is null || !RoleName.IsMatch(script.Role), "role must be a lower-case kebab-case name (the app's own role, e.g. \"admin\")");
        problems.AddIf(script.Steps.Count == 0, "steps needs at least one step");
        problems.AddIf(script.Viewports.Count == 0, "viewports needs at least one viewport");
        problems.AddIf(script.Viewports.Any(static v => !Enum.IsDefined(v)), "viewports holds an unknown viewport");
        for (var i = 0; i < script.Steps.Count; i++)
        {
            CheckStep(script.Steps[i], i + 1, problems);
        }

        foreach (var problem in script.Login?.Problems() ?? [])
        {
            problems.Add(problem);
        }

        if (script.Planning is { } planning)
        {
            problems.AddIf(planning.ElapsedMs < 0 || planning.InputTokens < 0 || planning.OutputTokens < 0, "planning numbers must not be negative");
        }

        return problems.Into(script, EvidenceErrors.ScriptInvalid);
    }

    private static void CheckStep(ScriptStep? step, int number, Problems problems)
    {
        var at = string.Create(CultureInfo.InvariantCulture, $"step {number}");
        if (step is null)
        {
            problems.Add($"{at} is null");
            return;
        }

        foreach (var broken in StepRules.Problems(step))
        {
            problems.Add($"{at}: {broken}");
        }

        if (step.ExpectedState.Problem() is { } problem)
        {
            problems.Add($"{at}: {problem}");
        }
    }

    // The app's own role names differ per deployment, so any lower-case kebab-case name is a role.
    [GeneratedRegex("^[a-z][a-z0-9-]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RoleName { get; }
}
