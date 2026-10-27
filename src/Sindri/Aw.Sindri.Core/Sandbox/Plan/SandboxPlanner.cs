using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The pure plan builder (sandbox spec SB12): every adapter's Plan calls it and adds only its mechanism.</summary>
public static class SandboxPlanner
{
    private const int PlanVersion = 1;

    /// <summary>Builds the plan for one launch.</summary>
    /// <param name="mechanism">The mechanism of the calling adapter.</param>
    /// <param name="request">The launch.</param>
    /// <returns>The plan, or SND-LAUNCH-015 (can't be confined to this policy), -016 (the plan itself is unsafe) or -017 (the login is not from the chosen source).</returns>
    /// <exception cref="ArgumentException">A Step request carries no StepPolicy, or an attended one carries one: a programming error.</exception>
    public static Result<SandboxPlan> Build(SandboxMechanism mechanism, SandboxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireConsistentKind(request);

        var c = PlanContext.From(request);
        if (PlanChecks.Validate(c) is { } refusal)
        {
            return refusal;
        }

        var hooks = HookPermissionPolicy.ForSession(request.Host.InstalledHooks);
        var plan = new SandboxPlan(
            PlanVersion,
            mechanism,
            string.Empty,
            request.Attended,
            request.Launch,
            request.Paths.Worktree,
            request.RunNamespace,
            request.Paths.RunDirectory,
            PlanMounts.Build(c),
            new PlanNetwork(request.Settings?.Network?.Mode ?? SandboxDefaults.Network, c.Groups, request.Net.SindriPort, request.Net.ProxyPort, request.Net.NetDir),
            PlanEnvironment.Build(c),
            c.Limits,
            c.Github,
            c.Login,
            request.Settings?.Inner ?? SandboxDefaults.Inner,
            PlanEnvironment.Git(c),
            PlanPolicy.Build(c, hooks),
            hooks);
        return plan with { PlanHash = PlanHasher.Hash(plan, PerLaunchPaths(request)) };
    }

    private static void RequireConsistentKind(SandboxRequest request)
    {
        if (!request.Attended && request.Policy is null)
        {
            throw new ArgumentException("A Step launch carries its StepPolicy; only an attended session has none.", nameof(request));
        }

        if (request.Attended && request.Policy is not null)
        {
            throw new ArgumentException("An attended session has no StepPolicy: a Step launched with the attended flag is a mistake.", nameof(request));
        }
    }

    /// <summary>A resume or reopen reuses the stored plan; a missing plan is refused (SB1).</summary>
    /// <param name="stored">The stored plan, or null when it is gone.</param>
    /// <returns>The plan, or SND-LAUNCH-015 'no-stored-plan'.</returns>
    public static Result<SandboxPlan> FromStored(SandboxPlan? stored) =>
        stored is null ? SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.NoStoredPlan, "the stored plan is gone") : stored;

    private static List<(string Placeholder, string Path)> PerLaunchPaths(SandboxRequest request)
    {
        var paths = request.Paths;
        List<(string, string)> replace =
        [
            ("$RUN_DIR", paths.RunDirectory),
            ("$RUN_AREA", paths.RunArea),
            ("$RUN_NS", request.RunNamespace),
            ("$NET", request.Net.NetDir),
            ("$LAUNCH", request.Launch.Value),
        ];
        if (paths.Worktree is { } worktree)
        {
            replace.Add(("$WORKTREE", worktree));
            replace.Add(("$WORKTREE_NAME", worktree[(worktree.LastIndexOf('/') + 1)..]));
        }

        return replace;
    }
}
