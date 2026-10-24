using System.Text.RegularExpressions;
using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Scripts;

/// <summary>Which of a step's members belong to which action, and what each may hold (SKILL.md 'Step actions and login', 'Step targets').</summary>
internal static partial class StepRules
{
    internal static IEnumerable<string> Problems(ScriptStep step)
    {
        var action = step.Action;
        if (!Enum.IsDefined(action))
        {
            yield return "action is not a known action";
            yield break;
        }

        if (action != StepAction.Press && step.Target.Length == 0)
        {
            yield return "target is required";
        }

        foreach (var problem in MemberProblems(step))
        {
            yield return problem;
        }

        foreach (var problem in TargetProblems(step))
        {
            yield return problem;
        }
    }

    private static IEnumerable<string> MemberProblems(ScriptStep step) =>
        IndexProblems(step).Concat(DragProblems(step)).Concat(KeyProblems(step)).Concat(ScrollProblems(step));

    private static IEnumerable<string> IndexProblems(ScriptStep step)
    {
        if (step.Index is not { } index)
        {
            yield break;
        }

        if (index < 0)
        {
            yield return "index must not be negative";
        }
        else if (step.Action is StepAction.Goto or StepAction.ExpectVisible or StepAction.Drag || (step.Action == StepAction.Press && step.Target.Length == 0))
        {
            yield return "index is only allowed for click, fill, scroll and press-with-a-target steps";
        }
    }

    private static IEnumerable<string> DragProblems(ScriptStep step)
    {
        if (step.Action != StepAction.Drag)
        {
            if (step.To is not null || step.GrabOffset is not null || step.DropOffset is not null || step.HoldAlt is not null)
            {
                yield return "to, grabOffset, dropOffset and holdAlt belong to drag steps";
            }
        }
        else if (string.IsNullOrEmpty(step.To))
        {
            yield return "drag needs a destination ('to')";
        }
    }

    private static IEnumerable<string> KeyProblems(ScriptStep step)
    {
        if (step.Action != StepAction.Press)
        {
            if (step.Key is not null)
            {
                yield return "key belongs to press steps";
            }
        }
        else if (string.IsNullOrEmpty(step.Key))
        {
            yield return "press needs a key";
        }
    }

    private static IEnumerable<string> ScrollProblems(ScriptStep step)
    {
        if (step.Action == StepAction.Scroll && step.Value is { } value && !ScrollValue.IsMatch(value))
        {
            yield return "scroll value must be \"end\", \"start\", \"bottom\", \"top\" or a non-negative integer pixel offset";
        }
    }

    private static IEnumerable<string> TargetProblems(ScriptStep step)
    {
        // Prefixed targets read for click, fill and press; scroll and drag name bare test ids.
        var parsed = step.Action switch
        {
            StepAction.Click or StepAction.Fill => StepTarget.Parse(step.Target),
            StepAction.Press when step.Target.Length > 0 => StepTarget.Parse(step.Target),
            StepAction.Goto or StepAction.ExpectVisible or StepAction.Scroll or StepAction.Drag or StepAction.Press => null,
            _ => null,
        };
        if (parsed is { IsError: true })
        {
            yield return parsed.Error.Message;
        }

        if (step.Action is StepAction.Scroll or StepAction.Drag && step.Target.Length > 0 && !TestId.TryFrom(step.Target, out _))
        {
            yield return $"{step.ActionName} target must be a data-testid";
        }

        if (step.Action == StepAction.Drag && step.To is { Length: > 0 } to && !TestId.TryFrom(to, out _))
        {
            yield return "drag destination must be a data-testid";
        }
    }

    [GeneratedRegex("^(end|start|bottom|top|\\d+)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ScrollValue { get; }
}
