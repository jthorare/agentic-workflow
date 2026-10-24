using System.Globalization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>The checks of a screen's script.</summary>
internal static class DesktopStepRules
{
    private const int MaxTimeoutMs = 120_000;

    internal static void Check(IReadOnlyList<DesktopStep>? steps, string screenAt, Problems problems)
    {
        var at = screenAt + ".steps";
        if (steps is null || steps.Count == 0)
        {
            problems.Add($"{at}: must list at least one step");
            return;
        }

        problems.AddIf(steps.Any(static s => s is null), $"{at}: holds a null entry");
        var real = steps.Where(static s => s is not null).ToList();
        problems.AddIf(real.Skip(1).Any(static s => s.Action == DesktopAction.Launch), $"{at}: launch is only the first step");
        problems.AddIf(real.Where(static s => s.Action == DesktopAction.Screenshot).Take(2).Count() != 1 || real[^1].Action != DesktopAction.Screenshot, $"{at}: must end with exactly one screenshot (the frame that is compared)");
        for (var i = 0; i < real.Count; i++)
        {
            CheckStep(real[i], string.Create(CultureInfo.InvariantCulture, $"{at}[{i}]"), problems);
        }
    }

    private static void CheckStep(DesktopStep step, string at, Problems problems)
    {
        CheckShape(step, at, problems);
        problems.AddIf(step.TimeoutMs is < 1 or > MaxTimeoutMs, $"{at}.timeoutMs: must be 1 to {MaxTimeoutMs}");
        problems.AddIf(step.Target is { } target && (target.IsEmpty || target.Index < 0), $"{at}.target: name at least one of automationId, name, type, text, contains, index (index from 0)");
        problems.AddIf(step.Target is { } texts && texts.Texts.Any(IsBad), $"{at}.target: a value must not be empty or start with '--' (the driver's command line refuses it)");
        problems.AddIf(step.Value is { } value && IsBad(value), $"{at}.value: must not be empty or start with '--' (the driver's command line refuses it)");
        problems.AddIf(step.ExpectedState is not null && step.Target is null, $"{at}.expectedState: needs a target to look at");
        CheckExpected(step.ExpectedState, at, problems);
    }

    private static void CheckShape(DesktopStep step, string at, Problems problems)
    {
        var needsTarget = step.Action is DesktopAction.Click or DesktopAction.WaitFor;
        problems.AddIf(needsTarget && step.Target is null, $"{at}.target: click and wait-for need a target");
        problems.AddIf(step.Action == DesktopAction.Launch && (step.Target is not null || step.Value is not null || step.ExpectedState is not null), $"{at}: launch takes no target, value or expectedState");
        CheckValue(step, at, problems);
    }

    private static void CheckValue(DesktopStep step, string at, Problems problems)
    {
        problems.AddIf(step.Action == DesktopAction.Type && step.Value is null, $"{at}.value: type needs the text to enter");
        problems.AddIf(step.Action == DesktopAction.Key && step.Value is null, $"{at}.value: key needs the chord to press (Enter, Ctrl+K)");
        problems.AddIf(step.Action is not (DesktopAction.Type or DesktopAction.Key) && step.Value is not null, $"{at}: value belongs to type and key, replace to type");
        problems.AddIf(step.Action != DesktopAction.Type && step.Replace is not null, $"{at}: value belongs to type and key, replace to type");
        CheckWaits(step, at, problems);
    }

    private static void CheckWaits(DesktopStep step, string at, Problems problems)
    {
        problems.AddIf(step.Action == DesktopAction.WaitFor && step.ExpectedState is null, $"{at}.expectedState: wait-for needs the state to wait for");
        problems.AddIf(step.Action == DesktopAction.Screenshot && (step.ExpectedState is not null || step.TimeoutMs is not null), $"{at}: screenshot takes no expectedState or timeoutMs");
    }

    private static void CheckExpected(DesktopExpectedState? expected, string at, Problems problems)
    {
        if (expected is null)
        {
            return;
        }

        var known = DesktopExpectedState.States.Contains(expected.State, StringComparer.Ordinal);
        problems.AddIf(!known, $"{at}.expectedState.state: '{expected.State}' is not one of {string.Join(", ", DesktopExpectedState.States)}");
        problems.AddIf(known && DesktopExpectedState.NeedsText(expected.State) != (expected.Text is not null), $"{at}.expectedState.text: given for textEquals and textContains only, and required for them");
        problems.AddIf(expected.Text is { } text && IsBad(text), $"{at}.expectedState.text: must not be empty or start with '--'");
    }

    private static bool IsBad(string text) => text.Length == 0 || text.StartsWith("--", StringComparison.Ordinal);
}
