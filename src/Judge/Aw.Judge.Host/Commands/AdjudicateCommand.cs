using System.Globalization;
using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.Adjudicate;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge adjudicate &lt;question&gt; [--limit 60]': a stronger model (claude on opus, high effort) labels the question's
/// unlabelled items, a label only on a 2-of-3 majority. The summary (labeled, split, failed) is JSON on stdout, exit 0. A
/// question the judge doesn't ask, a bad --limit and a --providers list that leaves out claude-cli are usage errors (64); the
/// claude CLI not being on PATH, or the store being unusable, is exit 1.
/// </summary>
internal static class AdjudicateCommand
{
    internal const string Name = "adjudicate";
    internal const string Synopsis = "adjudicate <question> [--limit <n>]";

    private const int DefaultLimit = 60;
    private const string LimitFlag = "--limit";

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyCollection<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        var parsed = Parse(args);
        if (parsed is null)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var request = new AdjudicateRequest(parsed.Value.Question, parsed.Value.Limit, allowed?.ToList());
        var service = services.GetRequiredService<IService<AdjudicateRequest, AdjudicateResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return CommandOutput.Emit(0, CommandOutput.Json(result.Value, json));
        }

        var usage = result.Error.Code == JudgeErrors.Invalid.Code || result.Error.Code == JudgeErrors.ProviderExcluded.Code;
        return usage ? CommandOutput.Usage(Synopsis, result.Error.Message) : CommandOutput.Emit(1, "", result.Error.Message);
    }

    // The question and the limit; null for any other shape (a missing question, a repeated or unknown flag, a limit that is not a whole number of at least 1).
    private static (string Question, int Limit)? Parse(IReadOnlyList<string> args)
    {
        string? question = null;
        int? limit = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], LimitFlag, StringComparison.Ordinal))
            {
                if (limit is not null || LimitAfter(args, i) is not { } value)
                {
                    return null;
                }

                limit = value;
                i++;
            }
            else if (question is null && !args[i].StartsWith('-'))
            {
                question = args[i];
            }
            else
            {
                return null;
            }
        }

        return question is null ? null : (question, limit ?? DefaultLimit);
    }

    // The whole number of at least 1 after the flag at the given position; null when there is none.
    private static int? LimitAfter(IReadOnlyList<string> args, int flagAt) =>
        (flagAt + 1) < args.Count && int.TryParse(args[flagAt + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : null;
}
