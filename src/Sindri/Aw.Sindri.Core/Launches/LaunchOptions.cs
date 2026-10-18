using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Launches;

/// <summary>The item's launch block (UI spec E-2, launcher L-15), decided from projections only (§6.2): the modes the server allows, the default agent and why, and what blocks an Open now.</summary>
public static class LaunchOptions
{
    /// <summary>Computes the block.</summary>
    /// <param name="inputs">The item, its launches, the last probe and the profile.</param>
    /// <returns>The block.</returns>
    public static ItemLaunch For(LaunchOptionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var launches = inputs.Launches;
        var inFlight = launches.FirstOrDefault(row => LaunchRules.InFlight(row, inputs.Now));
        var live = launches.FirstOrDefault(static row => row.Live);
        var lastStarted = launches.FirstOrDefault(static row => row.State == LaunchState.Started);
        var takenOver = inputs.Item.Engine?.Suspension == SuspensionKind.Takeover;
        var (target, chosenBy) = Choose(inputs);
        var resumable = ResumableFor(inputs, takenOver);
        var closed = ItemStates.IsClosed(inputs.Item.State);

        return new ItemLaunch(
            closed ? [] : Modes(inFlight is null, live is not null, resumable is not null),
            target,
            chosenBy,
            AttachTo(live),
            ResumeOf(resumable),
            inFlight?.Id,
            LastOf(lastStarted),
            closed || takenOver ? null : ConfirmFor(inputs),
            inputs.RepoPath,
            closed ? [] : Blockers(inputs, target));
    }

    private static List<LaunchMode> Modes(bool canOpen, bool canAttach, bool canResume)
    {
        var modes = new List<LaunchMode>(3);
        if (canOpen)
        {
            modes.Add(LaunchMode.Open);
        }

        if (canAttach)
        {
            modes.Add(LaunchMode.Attach);
        }

        if (canResume)
        {
            modes.Add(LaunchMode.Resume);
        }

        return modes;
    }

    private static LaunchAttachTarget? AttachTo(LaunchRow? live) => live is null ? null : new LaunchAttachTarget(live.Target, live.TabTitle, live.RequestedAt, live.Attach);

    private static LaunchResumable? ResumeOf(LaunchRow? row) => row is { ResumableId: { } id } ? new LaunchResumable(row.Target, id) : null;

    private static LaunchLastStarted? LastOf(LaunchRow? row) =>
        row is null ? null : new LaunchLastStarted(row.Target, row.RequestedAt, row.TabTitle, Public(row.Viewer), row.Late, row.Attach);

    private static LaunchViewerPublic? Public(LaunchViewer? viewer) => viewer is null ? null : new LaunchViewerPublic(viewer.Kind, viewer.Label, viewer.Outcome, viewer.Code);

    /// <summary>A blocker's text for a terminal detail: the line it points at, then the reason.</summary>
    private static string? Sentence(TerminalDetail? detail) =>
        detail is null ? null : (detail.Line is { } line ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"line {line}: ") : string.Empty) + detail.Reason;

    private static LaunchRow? ResumableFor(LaunchOptionInputs inputs, bool takenOver) =>
        inputs.Item.Engine is null || takenOver
            ? null
            : inputs.Launches.FirstOrDefault(static row => row.State == LaunchState.Started && !row.Live && row.ResumableId is not null);

    private static (AgentId Target, string ChosenBy) Choose(LaunchOptionInputs inputs)
    {
        if (inputs.LastUsedInRepo is { } last && inputs.InCatalog(last))
        {
            return (last, "lastUsed");
        }

        return ProfileDefault(inputs) is { } named ? (named, "profile") : (LaunchTargetChoice.Fallback, "builtIn");
    }

    private static AgentId? ProfileDefault(LaunchOptionInputs inputs)
    {
        var settings = inputs.Settings;
        var perRepo = inputs.Item.Repo is { } repo && settings?.Repos is { } repos && repos.TryGetValue(repo.Value, out var found) ? found.Default : null;
        if (perRepo is { } repoDefault && inputs.InCatalog(repoDefault))
        {
            return repoDefault;
        }

        return settings?.Default is { } profileDefault && inputs.InCatalog(profileDefault) ? profileDefault : null;
    }

    private static LaunchConfirm? ConfirmFor(LaunchOptionInputs inputs)
    {
        var window = inputs.Settings?.ConfirmWindow is { } text && RecipeDuration.TryFrom(text, out var parsed) ? parsed.Span : LaunchSettings.DefaultConfirmWindow;
        var recent = inputs.Launches.FirstOrDefault(row => MayHaveASession(row) && inputs.Now - row.RequestedAt < window);
        if (recent is null)
        {
            return null;
        }

        var minutes = (long)Math.Max(0, (inputs.Now - recent.RequestedAt).TotalMinutes);
        return new LaunchConfirm(string.Create(CultureInfo.InvariantCulture, $"A {recent.Target.Value} session for this item started {minutes} min ago. Open another one?"));
    }

    /// <summary>A started launch, or one that was interrupted or timed out (a session or a window may still have opened).</summary>
    private static bool MayHaveASession(LaunchRow row) =>
        row.State == LaunchState.Started
        || (row.State == LaunchState.Failed
            && (string.Equals(row.Code, SindriErrors.LaunchInterrupted.Code, StringComparison.Ordinal) || string.Equals(row.Code, SindriErrors.LaunchTimedOut.Code, StringComparison.Ordinal)));

    private static List<LaunchBlocker> Blockers(LaunchOptionInputs inputs, AgentId target)
    {
        var blockers = new List<LaunchBlocker>();
        if (inputs.RepoPath is null)
        {
            blockers.Add(new LaunchBlocker(SindriErrors.LaunchRepoMissing.Code, "the item's repo has no path in the approved profile (fix: add it to repos and approve)"));
        }

        if (inputs.Probe is not { Probed: true } probe)
        {
            return blockers;
        }

        if (probe.Terminal is { Present: false } terminal)
        {
            blockers.Add(string.Equals(terminal.Problem, "config-invalid", StringComparison.Ordinal)
                ? new LaunchBlocker(SindriErrors.LauncherInvalid.Code, Sentence(terminal.Detail) ?? "launcher.json isn't valid (fix: sindri launch show)")
                : new LaunchBlocker(SindriErrors.LaunchNoViewer.Code, Sentence(terminal.Detail) ?? "no terminal viewer is available here (fix: sindri launch init)"));
        }

        if (probe.Targets.FirstOrDefault(row => row.Facts.Id == target) is not { } row)
        {
            blockers.Add(new LaunchBlocker(SindriErrors.LaunchAgentMissing.Code, $"{target.Value} isn't in the catalog (fix: sindri launch show)"));
            return blockers;
        }

        if (!row.Facts.Present)
        {
            blockers.Add(new LaunchBlocker(SindriErrors.LaunchAgentMissing.Code, $"{target.Value} isn't installed on the login PATH"));
        }

        if (row.Budget.State == LaunchBudgetState.Hard)
        {
            blockers.Add(new LaunchBlocker(SindriErrors.LaunchBudgetReached.Code, $"{target.Value}'s daily budget is reached"));
        }

        return blockers;
    }
}
