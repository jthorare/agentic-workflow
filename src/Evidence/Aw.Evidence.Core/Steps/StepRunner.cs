using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;

namespace Aw.Evidence.Core.Steps;

/// <summary>
/// Runs one step and checks its expected state. The action failing means the selector is broken: a click or fill may
/// ask the judge for a visible replacement (at most <see cref="RepairRules.MaxAttempts"/> times), run the same action
/// on it and re-check the expectation (only a repaired action that fails gets another attempt; one that runs settles the
/// step: passed, or failed when the expectation misses); where writes are refused, a candidate that reads like a write is never acted on
/// (it counts as no candidate). The action passing but the expectation failing is a behaviour failure:
/// <see cref="StepStatus.Failed"/>, never repaired or masked by a model.
/// </summary>
/// <param name="judge">The judge that proposes repairs.</param>
/// <param name="clock">Times the repair calls.</param>
public sealed class StepRunner(IJudge judge, TimeProvider clock)
{
    /// <summary>Runs <paramref name="step"/>.</summary>
    /// <param name="session">The page.</param>
    /// <param name="step">The step.</param>
    /// <param name="options">Timeouts and whether repair is allowed.</param>
    /// <param name="log">Where repair calls are recorded.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>How the step ended.</returns>
    public async ValueTask<StepStatus> RunAsync(
        IBrowserSession session, ScriptStep step, StepRunOptions options, InvocationLog log, CancellationToken cancellationToken)
    {
        var acted = await PerformAsync(session, step, TargetOf(step), options.ActionTimeout, cancellationToken).ConfigureAwait(false);
        if (acted.IsError)
        {
            // No judge wired: no repair is tried, and no model call is recorded. A role: or text: target is the planner's to fix: a model never picks a replacement.
            return options.AllowRepair && RepairRules.IsRepairable(step.Action) && !StepTarget.IsPrefixed(step.Target) && judge.IsAvailable
                ? await RepairAsync(session, step, options, log, cancellationToken).ConfigureAwait(false)
                : StepStatus.Broken;
        }

        return await ExpectationHolds(session, step.ExpectedState, options.ExpectationTimeout, cancellationToken).ConfigureAwait(false)
            ? StepStatus.Passed
            : StepStatus.Failed;
    }

    /// <summary>Checks an expected state against the page.</summary>
    /// <param name="session">The page.</param>
    /// <param name="expected">The state.</param>
    /// <param name="timeout">How long it may take to hold.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether it holds.</returns>
    public static async ValueTask<bool> ExpectationHolds(IBrowserSession session, ExpectedState expected, TimeSpan timeout, CancellationToken cancellationToken)
    {
        switch (expected)
        {
            case InputValue input:
                var actual = await session.InputValueAsync(Locator.ByTestId(input.TestId), timeout, cancellationToken).ConfigureAwait(false);
                return actual.IsSuccess && string.Equals(actual.Value, input.Value, StringComparison.Ordinal);
            case TextVisible visible:
                return (await session.WaitVisibleAsync(Locator.ByText(visible.Text), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case TextAbsent absent:
                return (await session.WaitHiddenAsync(Locator.ByText(absent.Text), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case TestIdVisible element:
                return (await session.WaitVisibleAsync(Locator.ByTestId(element.TestId), timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case TestIdAbsent gone:
                // One immediate look: a leftover right after the action (a drag drop) must fail the step, so nothing waits for it to go.
                return (await session.ExpectAbsentNowAsync(Locator.ByTestId(gone.TestId), cancellationToken).ConfigureAwait(false)).IsSuccess;
            case UrlPath path:
                return (await session.WaitForPathAsync(path.Path, timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            case UrlSearch search:
                return (await session.WaitForUrlSearchAsync(search.Param, search.Value, timeout, cancellationToken).ConfigureAwait(false)).IsSuccess;
            default:
                return false;
        }
    }

    // A click or fill target, or a press's when it has one; null when it isn't a valid test id.
    private static Locator? TargetOf(ScriptStep step) => step.Action switch
    {
        StepAction.Click or StepAction.Fill or StepAction.Press => step.Target.Length > 0 && StepTarget.Parse(step.Target) is { IsSuccess: true } parsed
            ? Locator.For(parsed.Value, step.Index)
            : null,
        StepAction.ExpectVisible => Locator.ByText(step.Target),
        StepAction.Scroll => TestId.TryFrom(step.Target, out var id) ? (step.Index is { } nth ? new NthTestIdLocator(id, nth) : Locator.ByTestId(id)) : null,
        StepAction.Goto or StepAction.Drag => null,
        _ => null,
    };

    private static ValueTask<Result<Unit>> PerformAsync(
        IBrowserSession session, ScriptStep step, Locator? target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return step.Action switch
        {
            // Defence in depth behind StepGuard: a target never rewrites the host ('//evil', '@evil').
            StepAction.Goto => StepGuard.IsSameOriginPath(step.Target)
                ? session.GotoAsync(step.Target, timeout, cancellationToken)
                : Refused(EvidenceErrors.StepsRefused.With($"goto target must be a path on the host: {step.Target}")),
            StepAction.Drag => DragAsync(session, step, timeout, cancellationToken),
            // A press with no target goes to whatever has focus; one whose target can't be located sends no key.
            StepAction.Press => step.Target.Length > 0 && target is null
                ? Refused(EvidenceErrors.BrowserFailed.With("press has a blank target"))
                : session.PressAsync(step.Key ?? string.Empty, target, timeout, cancellationToken),
            StepAction.Click or StepAction.Fill or StepAction.Scroll or StepAction.ExpectVisible => target is null
                ? Refused(EvidenceErrors.BrowserFailed.With($"{step.ActionName} has a blank target"))
                : OnTargetAsync(session, step, target, timeout, cancellationToken),
            _ => Refused(EvidenceErrors.BrowserFailed.With($"unknown action {step.ActionName}")),
        };
    }

    private static ValueTask<Result<Unit>> Refused(Error error) => ValueTask.FromResult<Result<Unit>>(error);

    private static ValueTask<Result<Unit>> OnTargetAsync(
        IBrowserSession session, ScriptStep step, Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return step.Action switch
        {
            StepAction.Click => session.ClickAsync(target, timeout, cancellationToken),
            StepAction.Fill => session.FillAsync(target, step.Value ?? string.Empty, timeout, cancellationToken),
            StepAction.Scroll => session.ScrollAsync(target, step.Value ?? "end", timeout, cancellationToken),
            StepAction.Goto or StepAction.Drag or StepAction.Press or StepAction.ExpectVisible => session.WaitVisibleAsync(target, timeout, cancellationToken),
            _ => Refused(EvidenceErrors.BrowserFailed.With($"unknown action {step.ActionName}")),
        };
    }

    private static ValueTask<Result<Unit>> DragAsync(IBrowserSession session, ScriptStep step, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!TestId.TryFrom(step.Target, out var source) || !TestId.TryFrom(step.To ?? string.Empty, out var destination))
        {
            return ValueTask.FromResult<Result<Unit>>(EvidenceErrors.BrowserFailed.With("drag needs a source and a destination test id"));
        }

        var gesture = new DragGesture(step.GrabOffset, step.DropOffset, step.HoldAlt is true);
        return session.DragAsync(Locator.ByTestId(source), Locator.ByTestId(destination), gesture, timeout, cancellationToken);
    }

    private async ValueTask<StepStatus> RepairAsync(
        IBrowserSession session, ScriptStep step, StepRunOptions options, InvocationLog log, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RepairRules.MaxAttempts; attempt++)
        {
            var chosen = await ProposeAsync(session, step, log, cancellationToken).ConfigureAwait(false);
            if (chosen is null || !RepairRules.Fits(step.Action, chosen, options.RefuseWrites))
            {
                continue;
            }

            var target = new CandidateLocator(chosen.Index, chosen.TestId);
            var acted = await PerformAsync(session, step, target, options.ActionTimeout, cancellationToken).ConfigureAwait(false);
            if (acted.IsSuccess)
            {
                // The repaired action ran, so the page changed: the expectation decides, and nothing acts again.
                return await ExpectationHolds(session, step.ExpectedState, options.ExpectationTimeout, cancellationToken).ConfigureAwait(false)
                    ? StepStatus.Passed
                    : StepStatus.Failed;
            }
        }

        return StepStatus.Broken;
    }

    private async ValueTask<Candidate?> ProposeAsync(IBrowserSession session, ScriptStep step, InvocationLog log, CancellationToken cancellationToken)
    {
        var visible = await session.VisibleCandidatesAsync(cancellationToken).ConfigureAwait(false);
        if (visible.IsError)
        {
            return null;
        }

        var request = new SelectorRepairRequest(step.Target, step.Describe(), visible.Value);
        var proposal = await log.TimeAsync(
            InvocationPhase.SelectorRepair,
            clock,
            () => judge.RepairSelectorAsync(request, cancellationToken),
            static r => r.Usage).ConfigureAwait(false);
        return RepairRules.Resolve(proposal, visible.Value);
    }
}
