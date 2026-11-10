using Aw.Sindri.Core;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri answer &lt;run&gt;/&lt;node&gt; (--text T | --choice C) [--json]': a client of POST /sindri/workflow-runs/{run}/nodes/{node}/answer. Starts the control plane first, like the other workflow writes.
/// Exit 0 answered, 1 not running, 2 refused or error.
/// </summary>
internal static class WfAnswerCommand
{
    private const string Usage = "usage: sindri answer <run>/<node> (--text T | --choice C) [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (target, body, json, bad) = Parse(args);
        if (target is null || bad is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With((bad ?? "answer needs <run>/<node>") + $" ({Usage})")).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var cut = target.LastIndexOf('/');
        var route = $"/sindri/workflow-runs/{Uri.EscapeDataString(target[..cut])}/nodes/{Uri.EscapeDataString(target[(cut + 1)..])}/answer";
        var answered = await desk.Client.Post<AnswerQuestionBody, AnswerQuestionResponse>(route, body, ct).ConfigureAwait(false);
        if (answered.IsError)
        {
            return await output.Error(answered.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answered.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"{answered.Value.Value.Run.Value}/{answered.Value.Value.Node.Value} answered by {answered.Value.Value.AnsweredBy}\n", 0).ConfigureAwait(false);
    }

    private static (string? Target, AnswerQuestionBody Body, bool Json, string? Bad) Parse(string[] args)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal)).ToList();
        string? text = null;
        string? choice = null;
        var targets = new List<string>();
        for (var i = 0; i < rest.Count; i++)
        {
            if (IsValueFlag(rest[i]))
            {
                if (i + 1 >= rest.Count)
                {
                    return Refused($"'{rest[i]}' needs a value");
                }

                var value = rest[i + 1];
                (text, choice) = string.Equals(rest[i], "--text", StringComparison.Ordinal) ? (value, choice) : (text, value);
                i++;
            }
            else if (rest[i].StartsWith('-'))
            {
                return Refused($"'{rest[i]}' isn't an option");
            }
            else
            {
                targets.Add(rest[i]);
            }
        }

        var body = new AnswerQuestionBody(text, choice);
        return Check(targets, body) is { } bad ? Refused(bad) : (targets[0], body, json, null);

        (string?, AnswerQuestionBody, bool, string?) Refused(string reason) => (targets.Count > 0 ? targets[0] : null, new AnswerQuestionBody(text, choice), json, reason);
    }

    private static bool IsValueFlag(string arg) => string.Equals(arg, "--text", StringComparison.Ordinal) || string.Equals(arg, "--choice", StringComparison.Ordinal);

    private static string? Check(List<string> targets, AnswerQuestionBody body) => targets switch
    {
        [] => "answer needs <run>/<node>",
        [_, _, ..] => $"'{targets[1]}' is one argument too many",
        [var one] when !one.Contains('/', StringComparison.Ordinal) => $"'{one}' is not <run>/<node>",
        _ => (body.Text is null) == (body.Choice is null) ? "give exactly one of --text and --choice" : null,
    };
}
