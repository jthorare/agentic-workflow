using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.StartSkillRun;

/// <summary>
/// Runs one skill block as a trial (skill flows spec §7). It checks the inputs against the block, builds the block's trial recipe (<see cref="SkillTrialRecipe"/>), registers it, records an approval of its hash with the caller as approver
/// (the owner's request is the approval, and every start needs one: StartInstanceService), stores the inputs as start facts and starts the instance. The same block with the same inputs registers as the same recipe, so a second trial finds it
/// approved already. The launch reads the recipe's id (<see cref="SkillTrialRecipe.IsTrial"/>) to run scrubbed and mark 'trial'.
/// </summary>
/// <param name="blocks">The loaded skill blocks, and the one-folder load of the author loop.</param>
/// <param name="items">Resolves the item the trial runs on.</param>
/// <param name="recipes">Registers the trial recipe and approves its hash.</param>
/// <param name="facts">Where the input facts are stored.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="clock">When it happened.</param>
/// <param name="callers">The caller: the approver and the starter.</param>
internal sealed class StartSkillRunService(
    ISkillBlocks blocks,
    IItems items,
    TrialRecipes recipes,
    IFactStore facts,
    IInstanceRuntime runtime,
    TimeProvider clock,
    ICallerContextAccessor callers)
    : IService<StartSkillRunRequest, StartSkillRunResponse>
{
    /// <summary>The tenant every start runs under until the profile names one (as StartInstanceService).</summary>
    private const string Tenant = "local";

    public async ValueTask<Result<StartSkillRunResponse>> Handle(StartSkillRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("StartSkillRun needs a caller");
        }

        var block = Block(request);
        var checkedInputs = block.IsError ? block.Error : Refusal(block.Value, request.Inputs);
        if (checkedInputs is not null)
        {
            return checkedInputs;
        }

        var item = await items.Resolve(request.Item, cancellationToken).ConfigureAwait(false);
        var plan = item.IsError ? item.Error : SkillTrialRecipe.Build(block.Value, [.. request.Inputs.Keys], foldered: request.Skills is not null);
        var approved = plan.IsError ? plan.Error : await recipes.Approve(plan.Value, caller.Actor, cancellationToken).ConfigureAwait(false);
        var inputs = approved.IsError ? approved.Error : await Store(request, cancellationToken).ConfigureAwait(false);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var started = await runtime.Start(
            new InstanceStart(approved.Value, item.Value.Id, Attempt: 1, inputs.Value, Tenant, Ring: 0) { StartedAt = clock.GetUtcNow(), StartedBy = caller.Actor }, cancellationToken).ConfigureAwait(false);
        return started.IsError ? started.Error : new StartSkillRunResponse(started.Value, block.Value.Skill, block.Value.ManifestHash, [.. block.Value.Descriptor.Outputs.Select(static o => o.Fact.Value)], block.Value.Manifest.Outcomes.Success);
    }

    /// <summary>The block: from the loaded set, or, for the author loop, from the one folder the request sent (the loaded set is not touched).</summary>
    private Result<LoadedSkillBlock> Block(StartSkillRunRequest request)
    {
        var read = request.Skills is { } folder ? blocks.LoadFolder(folder) : new SkillBlocks(blocks.Loaded, blocks.Skipped);
        if (read.Loaded.FirstOrDefault(b => string.Equals(b.Manifest.Name, request.Block, StringComparison.Ordinal)) is { } found)
        {
            return found;
        }

        return read.Skipped.FirstOrDefault(s => string.Equals(s.Name, request.Block, StringComparison.Ordinal)) is { } skipped
            ? SindriErrors.InvalidInput.With($"block '{request.Block}' is not loaded: {skipped.Reason} (fix: edit {skipped.Path}, then try again)")
            : SindriErrors.InvalidInput.With($"no skill block '{request.Block}' (fix: sindri skills lists them)");
    }

    /// <summary>An input the block doesn't declare, a required one missing, or a value outside a declared list; null when the inputs fit.</summary>
    private static Error? Refusal(LoadedSkillBlock block, IReadOnlyDictionary<string, string> inputs)
    {
        var declared = block.Manifest.Inputs;
        if (inputs.Keys.FirstOrDefault(k => !declared.Any(d => string.Equals(d.Key, k, StringComparison.Ordinal))) is { } unknown)
        {
            return SindriErrors.InvalidInput.With($"block '{block.Manifest.Name}' has no input '{unknown}' (it takes: {string.Join(", ", declared.Select(static d => d.Key))})");
        }

        foreach (var input in declared)
        {
            _ = inputs.TryGetValue(input.Key, out var value);
            if (ValueRefusal(block.Manifest.Name, input, value) is { } refused)
            {
                return refused;
            }
        }

        return null;
    }

    /// <summary>A required input without a value, a bad PR reference, or a value outside a declared list; null when the value fits.</summary>
    private static Error? ValueRefusal(string block, SkillInput input, string? value)
    {
        if (input.Required && string.IsNullOrWhiteSpace(value))
        {
            return SindriErrors.InvalidInput.With($"block '{block}' needs input '{input.Key}' ({input.Label}) (fix: --input {input.Key}=<value>)");
        }

        if (value is not null && string.Equals(input.Key, PrReference.InputKey, StringComparison.Ordinal) && PrReference.Number(value) is { IsError: true } badPr)
        {
            return badPr.Error;   // SND-SKILL-006, before any recipe is made (the launch would refuse it too)
        }

        return value is not null && input.Values is { Count: > 0 } allowed && !allowed.Contains(value, StringComparer.Ordinal)
            ? SindriErrors.InvalidInput.With($"input '{input.Key}' of block '{block}' is one of: {string.Join(", ", allowed)}")
            : null;
    }

    /// <summary>Each input as a start fact, and the skills folder when the run has one.</summary>
    private async ValueTask<Result<IReadOnlyDictionary<FactKey, InputFact>>> Store(StartSkillRunRequest request, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, InputFact>();
        IEnumerable<(string Key, string Value)> all = request.Skills is { } folder
            ? [.. request.Inputs.Select(static i => ("fact:" + i.Key, i.Value)), (SkillTrialRecipe.SkillsFolderFact, folder)]
            : request.Inputs.Select(static i => ("fact:" + i.Key, i.Value));
        foreach (var (key, value) in all)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{key} is larger than 1 MiB (fix: pass a smaller value, or a path the skill can read)");
            }

            var put = await facts.Put(bytes, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }

            stored[FactKey.From(key)] = new InputFact("skill-run", put.Value);
        }

        return stored;
    }
}
