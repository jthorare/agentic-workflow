using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What the 'RunBlockStep' activity does (DT spec §8.2): resolves the Step's arguments and the facts it names, runs the registered
/// <see cref="IBlockStep"/> through <see cref="BlockStepDispatch"/>, and stores its outputs, so no fact content enters the workflow's history.
/// </summary>
/// <param name="runtimes">The Steps this build runs on the block runtime.</param>
/// <param name="catalog">The descriptors.</param>
/// <param name="dispatch">Runs a Step by id.</param>
/// <param name="contents">Reads a stored fact.</param>
/// <param name="facts">Stores an output.</param>
/// <param name="contexts">Gives a Step the sections and typed results of its inputs (kept in memory, or run again and checked against the recorded digests).</param>
/// <param name="approved">The APPROVED profile (never the live file): 'providers' resolves from its 'workflow.judge.providers'.</param>
internal sealed class BlockStepRunner(BlockStepRuntimes runtimes, BlockCatalog catalog, BlockStepDispatch dispatch, IFactContents contents, IFactStore facts, IApprovedProfile approved, StepContexts contexts)
{
    private const string ProvidersArgument = "providers";
    private const string ParallelArgument = "parallel";
    private const string ParallelKey = "workflow.judge.parallel";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>The registered Steps among <paramref name="used"/>, in id order.</summary>
    /// <param name="used">The Steps a recipe uses.</param>
    /// <returns>The ones this build runs on the block runtime.</returns>
    public IReadOnlyList<BlockId> Registered(IReadOnlySet<BlockId> used) =>
        [.. runtimes.Ids.Where(used.Contains).OrderBy(static id => id.Value, StringComparer.Ordinal)];

    /// <summary>Runs the Step once.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">The activity's cancellation.</param>
    /// <returns>The outcome with its stored outputs, or the failure.</returns>
    public async Task<BlockStepOutcome> Run(BlockStepRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var seeded = await Seed(run, catalog.Find(run.Step), cancellationToken).ConfigureAwait(false);
        if (seeded.IsError)
        {
            return Failed(seeded.Error);
        }

        var seed = seeded.Value;
        var context = run.Fills is { Count: > 0 } fills
            ? await contexts.For(new StepRun(fills[0].Request.Address, run.State, run.Token), seed, [.. fills.Select(static f => new InjectionInputSource(f.Recorded, f.Request.Context()))], cancellationToken).ConfigureAwait(false)
            : Results.Success(seed);
        if (context.IsError)
        {
            return Failed(context.Error);
        }

        var result = await dispatch(run.Step, context.Value, cancellationToken).ConfigureAwait(false);
        return result.Result is not { } done
            ? Failed(result.Failure ?? "the Step failed") with { Dependency = result.Dependency, Code = result.Code }
            : await StoreOutputs(done, cancellationToken).ConfigureAwait(false);
    }

    // The ledger is asked for a repo the instance does not name only when something reads it: a Step marked IRepoStep, one with Step inputs, or one that takes 'providers' or 'parallel'.
    private bool NeedsRepo(BlockStepRun run, BlockDescriptor? descriptor) =>
        runtimes.NeedsRepo(run.Step) || run.Fills is { Count: > 0 } || Takes(descriptor, ProvidersArgument) || Takes(descriptor, ParallelArgument);

    // What the Step starts with, before its inputs: its arguments, the facts it reads and the repo. The repo is resolved first: a Step never learns "no repo" from a repo it could not read.
    private async Task<Result<StepContext>> Seed(BlockStepRun run, BlockDescriptor? descriptor, CancellationToken cancellationToken)
    {
        var repo = await contexts.RepoOf(run.Address, run.Repo, run.RepoUnreadable, cancellationToken, NeedsRepo(run, descriptor)).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var resolved = await ResolveArguments(descriptor, run, repo.Value, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var named = await ReadFacts(descriptor, resolved.Value, run, cancellationToken).ConfigureAwait(false);
        if (named.IsError)
        {
            return named.Error;
        }

        return MissingFact(descriptor, named.Value, run.Step) is { } absent
            ? WorkflowErrors.BlockFactUnreadable.With(absent)
            : new StepContext(run.Step, resolved.Value, named.Value, InjectionSections.Empty)
            {
                Repo = repo.Value,
                Address = run.Address,
                HeavySlotHeld = run.HeavyHeld,
                LoopCaps = run.LoopCaps ?? new Dictionary<string, int>(StringComparer.Ordinal),
            };
    }

    // A Step that reads a fact the instance does not hold would take it for a rule that did not run.
    private static string? MissingFact(BlockDescriptor? descriptor, Dictionary<FactKey, JsonElement> named, BlockId step) =>
        descriptor?.Inputs.Where(k => !named.ContainsKey(k)).Select(static k => k.Value).ToList() is { Count: > 0 } missing
            ? $"{step.Value} needs {string.Join(", ", missing)}, which the instance does not hold"
            : null;

    private async Task<Result<Dictionary<string, JsonElement>>> ResolveArguments(BlockDescriptor? descriptor, BlockStepRun run, RepoName? repo, CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in run.Arguments)
        {
            if (value.Literal is { } literal)
            {
                arguments[name] = literal;
            }
            else if (value.Fact is { } key && run.Facts.TryGetValue(key, out var fact))
            {
                var read = await ReadText(key, fact, cancellationToken).ConfigureAwait(false);   // { fact }: the stored content, as the facts list is
                if (read.IsError)
                {
                    return read.Error;
                }

                arguments[name] = read.Value;
            }
        }

        var profiled = await ProfileArguments(descriptor, run, repo, arguments, cancellationToken).ConfigureAwait(false);
        return profiled.IsError ? profiled.Error : arguments;
    }

    private static bool Takes(BlockDescriptor? descriptor, string parameter) =>
        descriptor?.Parameters.Any(p => string.Equals(p.Name, parameter, StringComparison.Ordinal)) == true;

    private static bool TakesProfileWidth(BlockDescriptor? descriptor, BlockStepRun run) =>
        Takes(descriptor, ParallelArgument) && run.Arguments.TryGetValue(ParallelArgument, out var width) && string.Equals(width.Profile, ParallelKey, StringComparison.Ordinal);

    // 'providers' and a '{ profile: workflow.judge.parallel }' width come from the one approved-profile read, which happens only for a Step that takes either.
    private async Task<Result<bool>> ProfileArguments(BlockDescriptor? descriptor, BlockStepRun run, RepoName? repo, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        var takesProviders = Takes(descriptor, ProvidersArgument);
        var takesWidth = TakesProfileWidth(descriptor, run);
        if (!takesProviders && !takesWidth)
        {
            return false;
        }

        var profile = await ApprovedProfile(run, cancellationToken).ConfigureAwait(false);
        if (profile.IsError)
        {
            return profile.Error;
        }

        if (takesProviders)
        {
            arguments[ProvidersArgument] = JsonSerializer.SerializeToElement(JudgeProviders.Effective(profile.Value, repo?.Value, Literal(arguments)), _json);
        }

        if (takesWidth && JudgeParallel.Effective(profile.Value, repo?.Value) is { } parallel)
        {
            arguments[ParallelArgument] = JsonSerializer.SerializeToElement(parallel, _json);
        }

        return true;
    }

    private async Task<Result<JsonElement>> ReadText(FactKey key, FactValue fact, CancellationToken cancellationToken)
    {
        var read = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        return read.IsError
            ? WorkflowErrors.BlockFactUnreadable.With($"the fact {key.Value} can't be read: {read.Error.Message}")
            : JsonSerializer.SerializeToElement(Encoding.UTF8.GetString(read.Value.Span), _json);
    }

    // The APPROVED profile (never the live one): 'providers' (JudgeProviders) and a '{ profile: workflow.judge.parallel }' width resolve from it. Nothing approved means the defaults
    // (no jev, the Step's own width); an approved profile that can't be read fails the run, and the list is never widened.
    private async Task<Result<SindriProfile>> ApprovedProfile(BlockStepRun run, CancellationToken cancellationToken)
    {
        if (run.RepoUnreadable)
        {
            return WorkflowErrors.BlockRepoUnreadable.With("repo-unreadable");
        }

        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsError
            ? WorkflowErrors.BlockProfileUnreadable.With($"profile-unreadable: {latest.Error.Code}")
            : latest.Value?.Profile ?? new SindriProfile(1, string.Empty);
    }

    // The recipe's own provider list, which only narrows the profile's; { profile: workflow.judge.providers } carries none.
    private static IReadOnlyList<string>? Literal(Dictionary<string, JsonElement> arguments) =>
        arguments.TryGetValue(ProvidersArgument, out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(static n => n.ValueKind == JsonValueKind.String).Select(static n => n.GetString() ?? string.Empty)]
            : null;

    private async Task<Result<Dictionary<FactKey, JsonElement>>> ReadFacts(
        BlockDescriptor? descriptor, Dictionary<string, JsonElement> arguments, BlockStepRun run, CancellationToken cancellationToken)
    {
        var named = new Dictionary<FactKey, JsonElement>();
        foreach (var key in (descriptor?.Inputs ?? []).Concat(FactsNamed(descriptor, arguments)).Distinct())
        {
            if (!run.Facts.TryGetValue(key, out var fact))
            {
                continue;
            }

            var read = await ReadText(key, fact, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            named[key] = read.Value;
        }

        return named;
    }

    private async Task<BlockStepOutcome> StoreOutputs(StepResult done, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, FactValue>();
        foreach (var (key, value) in done.Outputs)
        {
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            var put = await facts.Put(Encoding.UTF8.GetBytes(text ?? string.Empty), cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return Failed(put.Error.Message);
            }

            stored[key] = put.Value;
        }

        return new BlockStepOutcome(done.Outcome, stored, DecisionOf(done.Outputs), Failure: null);
    }

    // An Unavailable error (a fact or the profile can't be read) is infrastructure: the engine retries the Step, then parks it as unavailable.
    private static BlockStepOutcome Failed(Error error) => Failed(error.Message) with { Dependency = error.Kind == ErrorKind.Unavailable ? "a fact or the profile" : null, Code = error.Kind == ErrorKind.Unavailable ? error.Code : null };

    private static BlockStepOutcome Failed(string reason) => new(Outcome: null, Outputs: null, Decision: null, reason);

    // The fact keys the Step's list parameter (InputsFrom, 'facts') names; the facts its descriptor reads (Inputs) come with them.
    private static IEnumerable<FactKey> FactsNamed(BlockDescriptor? descriptor, Dictionary<string, JsonElement> arguments) =>
        descriptor?.InputsFrom is { } list && arguments.TryGetValue(list, out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Where(static n => n.ValueKind == JsonValueKind.String).Select(static n => FactKey.From(n.GetString() ?? string.Empty))
            : [];

    // The Judge's decision id when 'fact:decision' carries one: the event's 'via block · decision <id>'.
    private static string? DecisionOf(IReadOnlyDictionary<FactKey, JsonElement> outputs) =>
        outputs.TryGetValue(FactKey.From("fact:decision"), out var decision) && decision.ValueKind == JsonValueKind.Object
            && decision.TryGetProperty("decisionId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
}
