using System.Globalization;
using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.GetLabelStatus;
using Aw.Judge.Features.ImportLabels;
using Aw.Judge.Features.LabelOutcomes;
using Aw.Judge.Features.SetLabel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge label import|outcomes|set|status': each subcommand binds its arguments, calls one service and prints the
/// response as JSON (exit 0). A bad flag, a missing or extra argument and an unknown subcommand are usage errors (64, one
/// 'usage: judge …' line); a service error is exit 1 with its message on stderr (an unknown item reads 'unknown item: &lt;id&gt;').
/// </summary>
internal static class LabelCommand
{
    internal const string Name = "label";
    internal const string Synopsis = "label import [--question <q>] [--since <n>d] | outcomes | set <itemId> <label|skip> | status";

    private const string Import = "import";
    private const string Outcomes = "outcomes";
    private const string Set = "set";
    private const string Status = "status";
    private const int MaxDays = 36_500;
    private static readonly TimeSpan _defaultSince = TimeSpan.FromDays(14);

    internal static async Task<int> Run(string[] args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var rest = args.Skip(1).ToArray();
        return args[0] switch
        {
            Import => await RunImport(rest, services, json).ConfigureAwait(false),
            Outcomes when rest.Length == 0 => await Call<LabelOutcomesRequest, LabelOutcomesResponse>(new LabelOutcomesRequest(), services, json).ConfigureAwait(false),
            Outcomes => CommandOutput.Usage("label outcomes"),
            Set => await RunSet(rest, services, json).ConfigureAwait(false),
            Status when rest.Length == 0 => await Call<GetLabelStatusRequest, GetLabelStatusResponse>(new GetLabelStatusRequest(), services, json).ConfigureAwait(false),
            Status => CommandOutput.Usage("label status"),
            _ => CommandOutput.Usage(Synopsis, $"unknown subcommand '{args[0]}'"),
        };
    }

    private static async Task<int> RunImport(string[] args, IServiceProvider services, JsonSerializerOptions json)
    {
        const string synopsis = "label import [--question <q>] [--since <n>d]";
        string? question = null;
        var since = _defaultSince;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
            {
                return CommandOutput.Usage(synopsis, $"{args[i]} needs a value");
            }

            switch (args[i])
            {
                case "--question" when args[i + 1].Length > 0:
                    question = args[i + 1];
                    break;
                case "--since" when TryDays(args[i + 1], out var days):
                    since = days;
                    break;
                case "--since":
                    return CommandOutput.Usage(synopsis, "--since is a number of days such as 14d");
                default:
                    return CommandOutput.Usage(synopsis, $"unknown argument '{args[i]}'");
            }
        }

        return await Call<ImportLabelsRequest, ImportLabelsResponse>(new ImportLabelsRequest(since, question), services, json).ConfigureAwait(false);
    }

    private static async Task<int> RunSet(string[] args, IServiceProvider services, JsonSerializerOptions json)
    {
        const string synopsis = "label set <itemId> <label|skip>";
        if (args.Length != 2 || !EvalItemId.TryFrom(args[0], out var item) || args[1].Length == 0)
        {
            return CommandOutput.Usage(synopsis, "needs an item id and a label");
        }

        return await Call<SetLabelRequest, SetLabelResponse>(new SetLabelRequest(item, args[1]), services, json).ConfigureAwait(false);
    }

    // '14d' or '14': whole days, at most a century (a larger window would overflow the clock's range).
    private static bool TryDays(string text, out TimeSpan since)
    {
        var digits = text.EndsWith('d') ? text[..^1] : text;
        var ok = int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days <= MaxDays;
        since = ok ? TimeSpan.FromDays(days) : default;
        return ok;
    }

    private static async Task<int> Call<TRequest, TResponse>(TRequest request, IServiceProvider services, JsonSerializerOptions json)
        where TRequest : notnull
    {
        var service = services.GetRequiredService<IService<TRequest, TResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value, json));
    }
}
