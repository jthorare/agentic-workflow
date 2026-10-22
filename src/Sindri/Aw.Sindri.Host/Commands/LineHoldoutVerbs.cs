using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Features.LineHoldout;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri line holdout show &lt;line&gt; &lt;phase&gt; [--json]' (line spec LN-API5): the owner's read of a phase's hidden scenarios. It is protected: the server answers counts and a challenge, the human types the
/// challenge at a terminal, and the request goes again with the answer; the server then marks what it showed spent. Exit 0 shown, 1 serve not running, 2 usage, a refusal or a failure.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
/// <param name="input">Where the typed answer is read.</param>
internal sealed class LineHoldoutVerbs(ControlPlaneClient client, Output output, bool interactive, TextReader input)
{
    private const string Usage = "usage: sindri line holdout show <line> <phase> [--json]";

    internal async Task<int> Run(IReadOnlyList<string> args, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal)).ToList();
        if (rest is not ["show", var line, var phase])
        {
            _ = await output.Error(SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
            return 2;
        }

        var request = new ShowLineHoldoutRequest(line, phase);
        var first = await client.Post<ShowLineHoldoutRequest, ShowLineHoldoutResponse>("/sindri/line/holdout/show", request, ct).ConfigureAwait(false);
        if (first.IsError)
        {
            return await Failed(first.Error).ConfigureAwait(false);
        }

        if (first.Value.Value.Challenge is not { } challenge)
        {
            return await Shown(first.Value, json).ConfigureAwait(false);
        }

        await output.Out.WriteAsync(Preview(first.Value.Value)).ConfigureAwait(false);
        var answer = await Answer(challenge).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        var done = await client.Post<ShowLineHoldoutRequest, ShowLineHoldoutResponse>("/sindri/line/holdout/show", request with { Answer = answer.Value }, ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await Shown(done.Value, json).ConfigureAwait(false);
    }

    private async Task<int> Shown(ApiAnswer<ShowLineHoldoutResponse> answer, bool json) =>
        json ? await output.Raw(answer.Body, 0).ConfigureAwait(false) : await output.Text(Scenarios(answer.Value), 0).ConfigureAwait(false);

    private static string Preview(ShowLineHoldoutResponse shown) => string.Create(CultureInfo.InvariantCulture,
        $"{shown.Line} {shown.Phase}: {shown.Total} hidden scenarios, {shown.Spent} already spent. Showing them marks each one spent: it no longer counts as hidden (PRV-HOLD-002).\n");

    private static string Scenarios(ShowLineHoldoutResponse shown)
    {
        var text = new StringBuilder(Preview(shown));
        foreach (var scenario in shown.Scenarios ?? [])
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"\n{scenario.Id}  {scenario.Kind}  item {scenario.Item ?? "-"}{(scenario.WasSpent ? "  (was spent)" : string.Empty)}\n");
            foreach (var step in scenario.Steps)
            {
                _ = text.Append(CultureInfo.InvariantCulture, $"  - {step}\n");
            }

            _ = text.Append(CultureInfo.InvariantCulture, $"  expected: {scenario.Expected}\n");
        }

        return text.ToString();
    }

    /// <summary>The human's answer to the challenge: what they typed at the terminal, sent as is (the server compares it).</summary>
    private async Task<Result<ChallengeAnswer>> Answer(HoldoutChallenge challenge)
    {
        if (!interactive)
        {
            return SindriErrors.AuthRefused.With($"this needs an interactive terminal: run it yourself and type '{challenge.Action}'; nothing was shown or marked spent");
        }

        await output.Out.WriteAsync($"Type '{challenge.Action}' to confirm: ").ConfigureAwait(false);
        var typed = (await input.ReadLineAsync().ConfigureAwait(false))?.Trim() ?? string.Empty;
        return new ChallengeAnswer(challenge.Challenge, typed);
    }

    private async Task<int> Failed(Error error) => await output.Error(error).ConfigureAwait(false);
}
