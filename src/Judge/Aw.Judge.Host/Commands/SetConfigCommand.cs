using System.Globalization;
using System.Text.Json;
using Aw.Judge.Features.SetConfig;
using Aw.Judge.Features.SetPromptSort;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge config set &lt;question&gt; enabled true|false | threshold &lt;0-1&gt;': writes config.json and prints the new
/// config. An unknown field, a value that doesn't parse or a missing argument is a usage error (64); a threshold
/// outside 0–1 or an invalid config.json is an error (1).
/// </summary>
internal static class SetConfigCommand
{
    internal const string Name = "set";
    internal const string Synopsis = "config set <question> enabled true|false | threshold <0-1>";
    private const string PromptSortSynopsis = "config set prompt-sort enabled true|false";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 3 || args[0].Length == 0)
        {
            return CommandOutput.Usage(Synopsis, "needs a question, a field and a value");
        }

        if (string.Equals(args[0], PromptSortCommands.Name, StringComparison.Ordinal))
        {
            return await SetPromptSort(args[1], args[2], services, json).ConfigureAwait(false);
        }

        var (request, problem) = Bind(args[0], args[1], args[2]);
        if (request is null)
        {
            return CommandOutput.Usage(Synopsis, problem);
        }

        var service = services.GetRequiredService<IService<SetConfigRequest, SetConfigResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }

    // 'config set prompt-sort enabled true|false': the sorter's own block, not a question's entry.
    private static async Task<int> SetPromptSort(string field, string value, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!string.Equals(field, "enabled", StringComparison.Ordinal) || value is not ("true" or "false"))
        {
            return CommandOutput.Usage(PromptSortSynopsis, "the prompt sorter takes only enabled true|false");
        }

        var service = services.GetRequiredService<IService<SetPromptSortRequest, SetPromptSortResponse>>();
        var result = await service.Handle(new SetPromptSortRequest(Enabled: string.Equals(value, "true", StringComparison.Ordinal)), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }

    private static (SetConfigRequest? Request, string? Problem) Bind(string question, string field, string value) => field switch
    {
        "enabled" => value switch
        {
            "true" => (new SetConfigRequest(question, Enabled: true), null),
            "false" => (new SetConfigRequest(question, Enabled: false), null),
            _ => (null, $"enabled takes true or false, not '{value}'"),
        },
        "threshold" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) && double.IsFinite(threshold)
            ? (new SetConfigRequest(question, Threshold: threshold), null)
            : (null, $"threshold takes a number, not '{value}'"),
        _ => (null, $"unknown field '{field}'"),
    };
}
