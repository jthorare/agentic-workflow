using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow answer &lt;address&gt; [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject |
/// --resume]', 'workflow command &lt;address&gt; &lt;verb&gt;', 'workflow cancel', 'takeover' and 'handback &lt;address&gt;' (the last two also as 'sindri takeover|handback &lt;item-or-address&gt;'), each with '[--nonce
/// &lt;n&gt;] [--json]' (engine spec rev 8 §5.2, §5.8, §6.3), as clients of the API: without '--nonce' the CLI asks the
/// instance's nonce route for one, then posts the command to its commands route, where the command chain classes the
/// caller. Each prints the new status header and what happened (an answer:
/// the resume action it ran); a repeated nonce prints 'already applied (…)'. A protected answer (grant, grant-tokens,
/// accept) is sent like any other: no typed phrase and no terminal (owner decision q93). Exit 0 applied, 1 not running, 2 invalid, refused or error.
/// </summary>
/// <param name="desk">The API client, the terminal and the start rule.</param>
/// <param name="services">The scope the clock resolves from.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class ApplyCommandVerbs(RecipeDesk desk, IServiceProvider services, Output output)
{
    private const string AnswerUsage =
        "usage: sindri workflow answer <address> [--text <t> | --file <f>] [--grant <n> | --grant-tokens <n> | --accept | --reject | --resume | --record <batch.json>] [--nonce <n>] [--json]";

    private const string RewindUsage = "usage: sindri workflow rewind <address> --to <state> [--nonce <n>] [--json]";

    private const string CommandUsage = "usage: sindri workflow command <address> <verb> [--nonce <n>] [--json]";

    private const string CancelUsage = "usage: sindri workflow cancel <address> [--nonce <n>] [--json]";

    private const string TakeoverUsage = "usage: sindri workflow takeover <address> [--nonce <n>] [--json]";

    private const string HandbackUsage = "usage: sindri workflow handback <address> [--nonce <n>] [--json]";

    private const string TakeoverShortUsage = "usage: sindri takeover <item-or-address> [--nonce <n>] [--json]";

    private const string HandbackShortUsage = "usage: sindri handback <item-or-address> [--nonce <n>] [--json]";

    /// <summary>The answer options that take no value.</summary>
    private static readonly string[] _switches = ["--accept", "--reject", "--resume"];

    /// <summary>The answer options that take one value.</summary>
    private static readonly string[] _valued = ["--text", "--file", "--grant", "--grant-tokens", "--nonce"];

    /// <summary>The answer options that carry text or the nonce rather than choose the variant.</summary>
    private static readonly string[] _carriers = ["--text", "--file", "--nonce"];

    internal async Task<int> Answer(string[] args, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, .. var options])
        {
            return await output.Error(SindriErrors.InvalidInput.With(AnswerUsage)).ConfigureAwait(false);
        }

        if (options.Contains("--record", StringComparer.Ordinal))
        {
            return await RecordAnswerCommand.Run(address, options, json, new RecordAnswerCommand.Host(desk, services, output), ct).ConfigureAwait(false);
        }

        var parsed = await AnswerOptions(options, ct).ConfigureAwait(false);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (variant, text, amount, nonce) = parsed.Value;
        var request = new ApplyCommandRequest(address, ApplyCommandKind.Answer, variant, text, amount, Verb: null, nonce, Challenge: null);
        return await Apply(request, json, ct).ConfigureAwait(false);
    }

    internal async Task<int> Command(string[] args, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, var verb, .. var options] || !CommandVerb.TryFrom(verb, out var parsed))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the verb matches [a-z][a-z-]{{1,31}} ({CommandUsage})")).ConfigureAwait(false);
        }

        var nonce = Nonce(options, CommandUsage);
        return nonce.IsError
            ? await output.Error(nonce.Error).ConfigureAwait(false)
            : await Apply(new ApplyCommandRequest(address, ApplyCommandKind.Command, Answer: null, Text: null, Amount: null, parsed, nonce.Value, Challenge: null), json, ct).ConfigureAwait(false);
    }

    /// <summary>'workflow rewind &lt;address&gt; --to &lt;state&gt;' (#1270): re-enters an earlier state and keeps the facts.</summary>
    internal async Task<int> Rewind(string[] args, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, .. var options] || options.ToList().IndexOf("--to") is not (>= 0 and var at) || at + 1 >= options.Length)
        {
            return await output.Error(SindriErrors.InvalidInput.With(RewindUsage)).ConfigureAwait(false);
        }

        var nonce = Nonce([.. options.Take(at), .. options.Skip(at + 2)], RewindUsage);
        return nonce.IsError
            ? await output.Error(nonce.Error).ConfigureAwait(false)
            : await Apply(new ApplyCommandRequest(address, ApplyCommandKind.Rewind, Answer: null, Text: null, Amount: null, Verb: null, nonce.Value, Challenge: null, To: options[at + 1]), json, ct).ConfigureAwait(false);
    }

    internal Task<int> Cancel(string[] args, CancellationToken ct) => Bare(args, ApplyCommandKind.Cancel, CancelUsage, ct);

    /// <summary>
    /// 'workflow takeover &lt;address&gt;' (class pause: an agent session may take over), or its short form 'sindri takeover
    /// &lt;item-or-address&gt;' (<paramref name="shortForm"/>; engine spec §6.3): a bare item is the root of its latest attempt.
    /// </summary>
    internal Task<int> Takeover(string[] args, CancellationToken ct, bool shortForm = false) =>
        Bare(args, ApplyCommandKind.Takeover, shortForm ? TakeoverShortUsage : TakeoverUsage, ct);

    /// <summary>
    /// 'workflow handback &lt;address&gt;' (class desk), or its short form
    /// 'sindri handback &lt;item-or-address&gt;' (<paramref name="shortForm"/>; engine spec §6.3): a bare item is the root of its latest attempt.
    /// </summary>
    internal Task<int> Handback(string[] args, CancellationToken ct, bool shortForm = false) =>
        Bare(args, ApplyCommandKind.Handback, shortForm ? HandbackShortUsage : HandbackUsage, ct);

    /// <summary>A command that takes only the address and the optional nonce: cancel, takeover, handback.</summary>
    private async Task<int> Bare(string[] args, ApplyCommandKind kind, string usage, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, .. var options])
        {
            return await output.Error(SindriErrors.InvalidInput.With(usage)).ConfigureAwait(false);
        }

        var nonce = Nonce(options, usage);
        return nonce.IsError
            ? await output.Error(nonce.Error).ConfigureAwait(false)
            : await Apply(new ApplyCommandRequest(address, kind, Answer: null, Text: null, Amount: null, Verb: null, nonce.Value, Challenge: null), json, ct).ConfigureAwait(false);
    }

    private async Task<int> Apply(ApplyCommandRequest request, bool json, CancellationToken ct)
    {
        var target = await InstanceTarget.Resolve(desk, request.Instance, request.Nonce, ct).ConfigureAwait(false);
        var done = target.IsError ? target.Error : await desk.Client.Post<ApplyCommandRequest, ApplyCommandResponse>(
            $"/sindri/workflow/instances/{target.Value.Id.Value}/commands", request with { Nonce = target.Value.Nonce }, ct).ConfigureAwait(false);
        if (done.IsError)
        {
            return await output.Error(done.Error).ConfigureAwait(false);
        }

        if (json)
        {
            return await output.Raw(done.Value.Body, 0).ConfigureAwait(false);
        }

        var answer = done.Value.Value;
        var happened = answer.AlreadyApplied ? $"already applied ({answer.Result.Happened})" : answer.Result.Happened;
        var header = InstanceHeader.Line(answer.Result.Instance, services.GetRequiredService<TimeProvider>().GetUtcNow());
        return await output.Text($"{header}\n  {happened}\n", 0).ConfigureAwait(false);
    }

    /// <summary>The answer's variant and its text or amount, and the optional nonce; exactly one variant, '--resume' may carry '--text'.</summary>
    internal static async Task<Result<(AnswerVariant Variant, string? Text, int? Amount, CommandNonce? Nonce)>> AnswerOptions(string[] options, CancellationToken ct)
    {
        var tokens = Tokens(options);
        if (tokens.IsError)
        {
            return tokens.Error;
        }

        string? Last(string flag) => tokens.Value.LastOrDefault(t => t.Flag == flag).Value;
        var text = Last("--file") is { } file ? await ReadText(file, ct).ConfigureAwait(false) : Last("--text");
        if (text.IsError)
        {
            return text.Error;
        }

        var variant = Variant(tokens.Value, text.Value is not null);
        if (variant.IsError)
        {
            return variant.Error;
        }

        if (Last("--nonce") is not { } nonce)
        {
            return (variant.Value.Variant, text.Value, variant.Value.Amount, null);
        }

        return CommandNonce.TryFrom(nonce, out var parsed)
            ? (variant.Value.Variant, text.Value, variant.Value.Amount, parsed)
            : SindriErrors.InvalidInput.With($"'{nonce}' isn't a nonce: 32 lower-case hex digits ({AnswerUsage})");
    }

    /// <summary>The options as '(flag, value)' pairs: '--accept', '--reject' and '--resume' take no value, the others one.</summary>
    private static Result<List<(string Flag, string? Value)>> Tokens(string[] options)
    {
        var tokens = new List<(string Flag, string? Value)>();
        for (var i = 0; i < options.Length; i++)
        {
            var flag = options[i];
            if (_switches.Contains(flag, StringComparer.Ordinal))
            {
                tokens.Add((flag, null));
            }
            else if (_valued.Contains(flag, StringComparer.Ordinal) && i + 1 < options.Length)
            {
                tokens.Add((flag, options[++i]));
            }
            else
            {
                return SindriErrors.InvalidInput.With($"'{flag}' isn't an option here, or needs a value ({AnswerUsage})");
            }
        }

        return tokens;
    }

    /// <summary>Exactly one variant: a grant (with its amount), '--accept', '--reject', '--resume' (which may carry the text), or the text alone.</summary>
    private static Result<(AnswerVariant Variant, int? Amount)> Variant(List<(string Flag, string? Value)> tokens, bool hasText)
    {
        var given = tokens.Where(static t => !_carriers.Contains(t.Flag, StringComparer.Ordinal)).ToList();
        var resumeWithText = given is [("--resume", _)];
        return (given, hasText) switch
        {
            ([], true) => (AnswerVariant.Text, null),
            ([("--accept", _)], false) => (AnswerVariant.Accept, null),
            ([("--reject", _)], false) => (AnswerVariant.Reject, null),
            ([("--resume", _)], _) when resumeWithText => (AnswerVariant.Resume, null),
            ([(var flag and ("--grant" or "--grant-tokens"), { } n)], false) when Amount(n, flag) is { } amount =>
                (flag == "--grant" ? AnswerVariant.Grant : AnswerVariant.GrantTokens, amount),
            _ => SindriErrors.InvalidInput.With($"give exactly one of --text/--file, --grant <n>, --grant-tokens <n>, --accept, --reject or --resume [--text] ({AnswerUsage})"),
        };
    }

    /// <summary>'--grant n' is a whole number; '--grant-tokens n' also takes '100k' or '1.5m'.</summary>
    private static int? Amount(string text, string flag) =>
        flag == "--grant"
            ? int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var rounds) ? rounds : null
            : TokenCounts.TryParse(text, out var tokens) ? tokens : null;

    private static async Task<Result<string?>> ReadText(string file, CancellationToken ct)
    {
        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{file} is larger than 1 MiB (fix: answer with a smaller file)");
            }

            return await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable file is the caller's input error
        {
            return SindriErrors.InvalidInput.With($"{file} can't be read: {e.Message}");
        }
    }

    /// <summary>The optional '--nonce &lt;n&gt;', the only option the command and cancel verbs take.</summary>
    private static Result<CommandNonce?> Nonce(string[] options, string usage) => options switch
    {
        [] => (CommandNonce?)null,
        ["--nonce", var given] when CommandNonce.TryFrom(given, out var nonce) => nonce,
        ["--nonce", var given] => SindriErrors.InvalidInput.With($"'{given}' isn't a nonce: 32 lower-case hex digits ({usage})"),
        _ => SindriErrors.InvalidInput.With($"'{string.Join(' ', options)}' isn't an option here ({usage})"),
    };

    private static (bool Json, string[] Others) Json(string[] args) =>
        (args.Contains("--json", StringComparer.Ordinal), [.. args.Where(static a => a != "--json")]);
}
