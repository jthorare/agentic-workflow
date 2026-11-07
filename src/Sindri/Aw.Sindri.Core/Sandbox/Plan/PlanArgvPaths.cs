using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The check that every absolute host path on the agent's command line is visible inside the sandbox (#2151).</summary>
public static class PlanArgvPaths
{
    /// <summary>Finds an absolute-path argument that a tmpfs or a deny hides in the final mount stack.</summary>
    /// <param name="plan">The plan the agent runs under.</param>
    /// <param name="argv">The agent's arguments.</param>
    /// <returns>The SND-LAUNCH-015 refusal naming the hidden path, or null when every path is visible.</returns>
    public static Error? Check(SandboxPlan plan, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(argv);
        var hidden = argv.FirstOrDefault(a => IsPath(a) && Hidden(plan, a));
        return hidden is null
            ? null
            : SandboxRefusal.For(SindriErrors.LaunchUnconfinable, SandboxReasons.ToolNotExpressible, $"the agent is given the path {hidden}, which the sandbox hides (fix: put the file where the plan binds it)");
    }

    // A prompt or a title is free text: only a single token that starts at the root is a path.
    private static bool IsPath(string argument) =>
        argument.Length > 1 && argument[0] == '/' && !argument.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));

    // The last mount over the path decides. A tmpfs or a deny hides it; /run is the sandbox's own empty directory (the net sockets), and a path no mount names is the sandbox's own root or /tmp.
    private static bool Hidden(SandboxPlan plan, string path)
    {
        var covering = plan.Mounts.LastOrDefault(m => string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path + "/", StringComparison.Ordinal));
        return covering is { Kind: PlanMountKind.Deny } or { Kind: PlanMountKind.Tmpfs, PrivateScratch: false } && !string.Equals(covering.Path, "/run", StringComparison.Ordinal);
    }
}
