using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
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
/// <param name="approved">The APPROVED profile (never the live file): 'providers' resolves from its 'workflow.judge.providers'.</param>
internal sealed class BlockStepRunner(BlockStepRuntimes runtimes, BlockCatalog catalog, BlockStepDispatch dispatch, IFactContents contents, IFactStore facts, IApprovedProfile approved)
{
    private const string ProvidersArgument = "providers";

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
        var descriptor = catalog.Find(run.Step);
        var resolved = await ResolveArguments(descriptor, run, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return Failed(resolved.Error.Message);
        }

        var arguments = resolved.Value;
        var named = await ReadFacts(descriptor, arguments, run, cancellationToken).ConfigureAwait(false);
        if (named.IsError)
        {
            return Failed(named.Error.Message);
        }

        var result = await dispatch(run.Step, new StepContext(run.Step, arguments, named.Value, InjectionSections.Empty), cancellationToken).ConfigureAwait(false);
        return result.Result is not { } done
            ? Failed(result.Failure ?? "the Step failed")
            : await StoreOutputs(done, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Dictionary<string, JsonElement>>> ResolveArguments(BlockDescriptor? descriptor, BlockStepRun run, CancellationToken cancellationToken)
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

        if (descriptor?.Parameters.Any(static p => p.Name == ProvidersArgument) == true)
        {
            var providers = await Providers(run, arguments, cancellationToken).ConfigureAwait(false);
            if (providers.IsError)
            {
                return providers.Error;
            }

            arguments[ProvidersArgument] = providers.Value;
        }

        return arguments;
    }

    private async Task<Result<JsonElement>> ReadText(FactKey key, FactValue fact, CancellationToken cancellationToken)
    {
        var read = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        return read.IsError
            ? new Error("SND-BLOCK-001", ErrorKind.Unavailable, $"the fact {key.Value} can't be read: {read.Error.Message}")
            : JsonSerializer.SerializeToElement(Encoding.UTF8.GetString(read.Value.Span), _json);
    }

    // The effective provider list (JudgeProviders): the APPROVED profile's, narrowed by the recipe's own literal list; { profile: workflow.judge.providers } narrows nothing.
    // Nothing approved means the defaults (no jev); an approved profile that can't be read fails the run, and the list is never widened.
    private async Task<Result<JsonElement>> Providers(BlockStepRun run, Dictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return new Error("SND-BLOCK-002", ErrorKind.Unavailable, $"profile-unreadable: {latest.Error.Code}");
        }

        var profile = latest.Value?.Profile ?? new SindriProfile(1, string.Empty);
        IReadOnlyList<string>? literal = arguments.TryGetValue(ProvidersArgument, out var named) && named.ValueKind == JsonValueKind.Array
            ? [.. named.EnumerateArray().Where(static n => n.ValueKind == JsonValueKind.String).Select(static n => n.GetString() ?? string.Empty)]
            : null;
        return JsonSerializer.SerializeToElement(JudgeProviders.Effective(profile, run.Repo, literal), _json);
    }

    private async Task<Result<Dictionary<FactKey, JsonElement>>> ReadFacts(
        BlockDescriptor? descriptor, Dictionary<string, JsonElement> arguments, BlockStepRun run, CancellationToken cancellationToken)
    {
        var named = new Dictionary<FactKey, JsonElement>();
        foreach (var key in FactsNamed(descriptor, arguments))
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

    private static BlockStepOutcome Failed(string reason) => new(Outcome: null, Outputs: null, Decision: null, reason);

    // The fact keys the Step's list parameter (InputsFrom, 'facts') names.
    private static IEnumerable<FactKey> FactsNamed(BlockDescriptor? descriptor, Dictionary<string, JsonElement> arguments) =>
        descriptor?.InputsFrom is { } list && arguments.TryGetValue(list, out var names) && names.ValueKind == JsonValueKind.Array
            ? names.EnumerateArray().Where(static n => n.ValueKind == JsonValueKind.String).Select(static n => FactKey.From(n.GetString() ?? string.Empty))
            : [];

    // The Judge's decision id when 'fact:decision' carries one: the event's 'via block · decision <id>'.
    private static string? DecisionOf(IReadOnlyDictionary<FactKey, JsonElement> outputs) =>
        outputs.TryGetValue(FactKey.From("fact:decision"), out var decision) && decision.ValueKind == JsonValueKind.Object
            && decision.TryGetProperty("decisionId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
}
