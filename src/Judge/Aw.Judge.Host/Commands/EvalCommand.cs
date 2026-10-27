using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Features.ImportTurns;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge eval &lt;question&gt; [flags]' scores one provider against the labels (<see cref="EvaluateQuestionService"/>'s slice); 'judge
/// eval import-turns &lt;transcript.jsonl&gt;... [--since 14d]' imports turn-progress items. Only import-turns, spelled exactly, is the
/// second word: any other first word is a question name, so a mistyped 'eval import-turn' fails as an unknown question.
/// A flag the command doesn't know, a flag without its value, a missing or extra question and no transcript file are usage
/// errors (64), and so is a provider the allowlist excludes; an unknown provider, a bad --labels or --since value and any
/// other failure exit 1.
/// </summary>
internal static class EvalCommand
{
    internal const string Name = "eval";
    internal const string ImportTurns = "import-turns";

    private const string EvalSynopsis = "eval <question> [--provider P] [--variant V] [--labels outcome|adjudicator|override|any] [--collapse LABEL] [--record FILE] [--replay FILE]";
    private const string ImportSynopsis = "eval import-turns <transcript.jsonl>... [--since 14d]";
    private const string DefaultProvider = "jev";

    private static readonly string[] _evalFlags = ["--provider", "--variant", "--labels", "--collapse", "--record", "--replay"];

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyList<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json) =>
        args.Count > 0 && string.Equals(args[0], ImportTurns, StringComparison.Ordinal)
            ? await RunImport([.. args.Skip(1)], services, json).ConfigureAwait(false)
            : await RunEval(args, allowed, services, json).ConfigureAwait(false);

    private static async Task<int> RunEval(IReadOnlyList<string> args, IReadOnlyList<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        var (positional, flags, problem) = Parse(args, _evalFlags);
        if (problem is not null)
        {
            return CommandOutput.Usage(EvalSynopsis, problem);
        }

        if (positional.Count != 1)
        {
            return CommandOutput.Usage(EvalSynopsis, positional.Count == 0 ? "a question name is required" : "one question at a time");
        }

        return await Score(positional[0], flags, allowed, services, json).ConfigureAwait(false);
    }

    private static async Task<int> Score(
        string question, Dictionary<string, string> flags, IReadOnlyList<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!ProviderNames.TryParse(flags.GetValueOrDefault("--provider", DefaultProvider), out var provider))
        {
            return CommandOutput.Emit(1, "", $"provider not available: {flags["--provider"]}");
        }

        var labels = Labels(flags.GetValueOrDefault("--labels"));
        if (labels.IsError)
        {
            return CommandOutput.Emit(1, "", labels.Error.Message);
        }

        var request = new EvaluateQuestionRequest(
            question,
            provider,
            flags.GetValueOrDefault("--variant", "as-is"),
            labels.Value,
            flags.GetValueOrDefault("--collapse"),
            flags.GetValueOrDefault("--record"),
            flags.GetValueOrDefault("--replay"),
            allowed);
        var result = await services.GetRequiredService<IService<EvaluateQuestionRequest, EvaluateQuestionResponse>>().Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? Failed(result.Error) : CommandOutput.Emit(0, result.Value.ToJson().ToJsonString(json));
    }

    // 'any' (or no flag) is no source; the rest are label source names.
    private static Result<LabelSource?> Labels(string? source)
    {
        if (source is null || string.Equals(source, "any", StringComparison.Ordinal))
        {
            return Results.Success<LabelSource?>(value: null);
        }

        return LabelSources.TryParse(source, out var named)
            ? Results.Success<LabelSource?>(named)
            : JudgeErrors.Invalid.With("--labels must be outcome, adjudicator, override or any");
    }

    private static async Task<int> RunImport(string[] args, IServiceProvider services, JsonSerializerOptions json)
    {
        var (positional, flags, problem) = Parse(args, ["--since"]);
        if (problem is not null)
        {
            return CommandOutput.Usage(ImportSynopsis, problem);
        }

        if (positional.Count == 0)
        {
            return CommandOutput.Usage(ImportSynopsis, "a transcript file is required");
        }

        TimeSpan? since = null;
        if (flags.TryGetValue("--since", out var window))
        {
            if (!window.EndsWith('d') || !int.TryParse(window.AsSpan(..^1), NumberStyles.None, CultureInfo.InvariantCulture, out var days))
            {
                return CommandOutput.Emit(1, "", "--since must look like 14d");
            }

            since = TimeSpan.FromDays(days);
        }

        var result = await services.GetRequiredService<IService<ImportTurnsRequest, ImportTurnsResponse>>()
            .Handle(new ImportTurnsRequest(positional, since), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? Failed(result.Error)
            : CommandOutput.Emit(0, new JsonObject
            {
                ["imported"] = result.Value.Imported,
                ["skipped"] = result.Value.Skipped,
                ["unreadable"] = result.Value.Unreadable,
            }.ToJsonString(json));
    }

    // An allowlist that leaves the provider out is a usage error; any other failure is an error.
    private static int Failed(Error error) =>
        string.Equals(error.Code, JudgeErrors.ProviderExcluded.Code, StringComparison.Ordinal)
            ? CommandOutput.Emit(CommandOutput.UsageExit, "", error.Message.ReplaceLineEndings(" "))
            : CommandOutput.Emit(1, "", error.Message.ReplaceLineEndings(" "));

    // Positional words and '--flag value' pairs, in any order; a repeated flag keeps its last value.
    private static (List<string> Positional, Dictionary<string, string> Flags, string? Problem) Parse(IReadOnlyList<string> args, string[] known)
    {
        List<string> positional = [];
        Dictionary<string, string> flags = new(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) && !string.Equals(args[i], "-h", StringComparison.Ordinal))
            {
                positional.Add(args[i]);
            }
            else if (!known.Contains(args[i], StringComparer.Ordinal))
            {
                return (positional, flags, $"unknown option '{args[i]}'");
            }
            else if (i + 1 >= args.Count)
            {
                return (positional, flags, $"{args[i]} needs a value");
            }
            else
            {
                flags[args[i]] = args[++i];
            }
        }

        return (positional, flags, null);
    }
}
