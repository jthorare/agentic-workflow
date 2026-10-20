using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks of the line's profile keys (line spec LN-WF9, Appendix D): ids and sizes are the line's words, numbers are positive, and the one cross-key rule holds the line's parallelism to the box's heavy-slot cap.</summary>
internal static class LineProfileRules
{
    private static readonly string[] _sizes = ["XS", "S", "M", "L", "XL"];
    private static readonly string[] _phaseSizes = ["XS", "S", "M"];
    private const int MaxCommand = 1000;

    /// <summary>The first problem in the line's keys, or none.</summary>
    /// <param name="workflow">The 'workflow:' block, or null.</param>
    /// <param name="box">The box's limits, or null to check the profile alone.</param>
    internal static Error? Check(WorkflowSettings? workflow, BoxLimits? box) =>
        Packs(workflow) ?? Line(workflow, box) ?? Plan(workflow) ?? Prove(workflow) ?? Build(workflow) ?? DesignModels(workflow) ?? ReviewModels(workflow) ?? Steps(workflow) ?? Docs(workflow);

    private static Error? Packs(WorkflowSettings? workflow)
    {
        var packs = workflow?.Packs;
        return Choice("workflow.packs.style", packs?.Style) ?? LensChoices(packs?.Lenses) ?? Pins(packs?.Pins);
    }

    private static Error? LensChoices(LensPackSettings? lenses) =>
        Choice("workflow.packs.lenses.design", lenses?.Design) ?? Choice("workflow.packs.lenses.review", lenses?.Review);

    private static Error? Choice(string path, PackChoice? choice)
    {
        if (choice?.Default is { } id && !IsPackId(id))
        {
            return Invalid($"{path}.default", $"'{id}' is not a pack id: a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters");
        }

        foreach (var (repo, pack) in choice?.Repos ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                return Invalid($"{path}.repos", "a repo name must be non-empty text");
            }

            if (!IsPackId(pack))
            {
                return Invalid($"{path}.repos.{repo}", $"'{pack}' is not a pack id: a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters");
            }
        }

        return null;
    }

    private static Error? Pins(IReadOnlyDictionary<string, string>? pins)
    {
        foreach (var (name, hash) in pins ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            var parts = name.Split('/');
            if (parts is not ([var kind, var id]) || kind is not ("style" or "lenses") || !IsPackId(id))
            {
                return Invalid($"workflow.packs.pins.{name}", "is not a pin: 'style/<id>' or 'lenses/<id>'");
            }

            if (hash.Length != 71 || !hash.StartsWith("sha256:", StringComparison.Ordinal) || !hash[7..].All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return Invalid($"workflow.packs.pins.{name}", "must be 'sha256:' and 64 lower-case hex digits");
            }
        }

        return null;
    }

    private static Error? Line(WorkflowSettings? workflow, BoxLimits? box)
    {
        var line = workflow?.Line;
        foreach (var (key, value) in new[] { ("parallel", line?.Parallel), ("maxOpenPrs", line?.MaxOpenPrs), ("maxStack", line?.MaxStack) })
        {
            if (value is < 1)
            {
                return Invalid($"workflow.line.{key}", "must be 1 or more");
            }
        }

        return ParallelCap(line?.Parallel, box);
    }

    // A parallel left out is the default and is not held to the cap: a box with one slot still runs a profile that never mentions the line.
    private static Error? ParallelCap(int? parallel, BoxLimits? box) =>
        parallel is { } wanted && box is not null && wanted > box.HeavySlots
            ? Invalid("workflow.line.parallel", string.Create(CultureInfo.InvariantCulture, $"is {wanted}, above this box's heavy-slot cap of {box.HeavySlots} (fix: lower it, or raise the cap in $AW_STATE_DIR/heavy-slots-cap)"))
            : null;

    private static Error? Plan(WorkflowSettings? workflow)
    {
        var plan = workflow?.Plan;
        if (plan?.MaxPhaseSize is { } max && Array.IndexOf(_phaseSizes, max) < 0)
        {
            return Invalid("workflow.plan.maxPhaseSize", $"'{max}' is not XS, S or M: a phase larger than M is split");
        }

        foreach (var (size, limits) in plan?.Sizes ?? new Dictionary<string, SizeSettings>(StringComparer.Ordinal))
        {
            if (Array.IndexOf(_sizes, size) < 0)
            {
                return Invalid($"workflow.plan.sizes.{size}", "is not a size: XS, S, M, L or XL");
            }

            if (Negative($"workflow.plan.sizes.{size}", limits) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? Negative(string path, SizeSettings limits)
    {
        (string Key, long? Value)[] numbers =
        [
            ("files", limits.Files), ("items", limits.Items), ("budget.tokens", limits.Budget?.Tokens), ("budget.turns", limits.Budget?.Turns), ("budget.wallMinutes", limits.Budget?.WallMinutes),
        ];
        foreach (var (key, value) in numbers)
        {
            if (value is < 0)
            {
                return Invalid($"{path}.{key}", "must be 0 or more");
            }
        }

        return null;
    }

    private static Error? Prove(WorkflowSettings? workflow) =>
        workflow?.Prove?.HoldoutMin is < 1 ? Invalid("workflow.prove.holdoutMin", "must be 1 or more") : null;

    private static Error? Build(WorkflowSettings? workflow)
    {
        if (workflow?.Build?.Commands is not { } commands)
        {
            return null;
        }

        if (commands.Count == 0)
        {
            return Invalid("workflow.build.commands", "must list at least one command (or leave the key out)");
        }

        for (var i = 0; i < commands.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(commands[i]) || commands[i].Length > MaxCommand || commands[i].IndexOfAny(['\n', '\r', '\0']) >= 0)
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"workflow.build.commands[{i}]"), $"must be one command line of 1 to {MaxCommand} characters, no line break");
            }
        }

        return null;
    }

    private static Error? DesignModels(WorkflowSettings? workflow) => Models("workflow.design.models", workflow?.Design?.Models?.Writer, workflow?.Design?.Models?.Lens);

    private static Error? ReviewModels(WorkflowSettings? workflow) => Models("workflow.review.models", writer: null, workflow?.Review?.Models?.Lens);

    private static Error? Models(string path, string? writer, string? lens)
    {
        foreach (var (key, id) in new[] { ("writer", writer), ("lens", lens) })
        {
            if (id is not null && !Launch.AgentId.TryParse(id, provider: null, out _))
            {
                return Invalid($"{path}.{key}", $"'{id}' is not an agent id: a lowercase letter, then lowercase letters, digits or '-', 2 to 32 characters");
            }
        }

        return null;
    }

    private static Error? Steps(WorkflowSettings? workflow) =>
        workflow?.Steps?.LongMax is { } longest && !RecipeDuration.TryFrom(longest, out _)
            ? Invalid("workflow.steps.longMax", $"'{longest}' is not a duration: a whole number and m, h or d (for example 2h)")
            : null;

    private static Error? Docs(WorkflowSettings? workflow)
    {
        if (workflow?.Docs?.Globs is not { } globs)
        {
            return null;
        }

        if (globs.Count == 0)
        {
            return Invalid("workflow.docs.globs", "must list at least one glob (or leave the key out)");
        }

        for (var i = 0; i < globs.Count; i++)
        {
            if (ProfileRules.GlobProblem(globs[i]) is { } problem)
            {
                return Invalid(string.Create(CultureInfo.InvariantCulture, $"workflow.docs.globs[{i}]"), problem);
            }
        }

        return null;
    }

    private static bool IsPackId(string id) =>
        id.Length is >= 2 and <= 40 && id[0] is >= 'a' and <= 'z' && id.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
