using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow complete &lt;address&gt; --run &lt;t&gt; --outcome &lt;o&gt; [--output &lt;fact&gt;=&lt;file&gt;]… [--nonce
/// &lt;n&gt;] [--json]' (engine spec rev 8.1 §5.3, §6.3; engine P4 unit (c)). The CLI reads each output file (at most
/// 1 MiB) and sends its content, never its path, to the API's complete route, where the command chain classes the caller
/// (an agent session is refused SND-AUTH-002). It prints the new status header and what happened ('-> checks', or
/// 'already applied (-> checks)' for a repeated nonce); '--json' prints the response with the nonce it used. Without
/// '--nonce' the instance's nonce route issues one first. A launched session (AW_SINDRI_LAUNCH_SECRET set) waits out a control plane that is down, see <see cref="ConnectRetry"/>. Exit 0 applied, 1 not running, 2 invalid, refused (SND-WF-005, -008, -009 …) or error.
/// </summary>
internal static class CompleteCommand
{
    private const string SecretVariable = "AW_SINDRI_LAUNCH_SECRET";

    private const string Usage =
        "usage: sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct)
    {
        var (request, json, problem) = await Parse(args, ct).ConfigureAwait(false);
        if (request is null)
        {
            return await output.Error(problem ?? SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var clock = services.GetRequiredService<TimeProvider>();
        var done = await Attempt(desk, request, clock, output, ct).ConfigureAwait(false);
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

    /// <summary>
    /// Resolves the instance and posts the report. A launched session (its proof is set) waits out a control plane that is down or restarting, see <see cref="ConnectRetry"/>,
    /// and keeps the nonce it was issued across the waits, so a report that did land is never sent as a second command.
    /// </summary>
    private static Task<Result<ApiAnswer<CompleteStepResponse>>> Attempt(RecipeDesk desk, CompleteStepRequest request, TimeProvider clock, Output output, CancellationToken ct)
    {
        var nonce = request.Nonce;
        async Task<Result<ApiAnswer<CompleteStepResponse>>> Once()
        {
            var target = await InstanceTarget.Resolve(desk, request.Instance, nonce, ct).ConfigureAwait(false);
            if (target.IsError)
            {
                return target.Error;
            }

            nonce = target.Value.Nonce;
            return await Send(desk, target.Value, request, ct).ConfigureAwait(false);
        }

        return desk.Environment?.Invoke(SecretVariable) is { Length: > 0 } ? ConnectRetry.Run(Once, clock, output.Notice, ct) : Once();
    }

    /// <summary>Posts the report; a launched session's carries AW_SINDRI_LAUNCH_SECRET as the X-Sindri-Launch-Secret header (never in the body).</summary>
    private static Task<Result<ApiAnswer<CompleteStepResponse>>> Send(RecipeDesk desk, (WorkflowInstanceId Id, CommandNonce Nonce) target, CompleteStepRequest request, CancellationToken ct)
    {
        var route = $"/sindri/workflow/instances/{target.Id.Value}/runs/{InstanceRoute.Segment(request.Run.Value)}/complete";
        var report = request with { Nonce = target.Nonce };
        return desk.Environment?.Invoke(SecretVariable) is { Length: > 0 } secret
            ? desk.Client.PostProven<CompleteStepRequest, CompleteStepResponse>(route, report, secret, ct)
            : desk.Client.Post<CompleteStepRequest, CompleteStepResponse>(route, report, ct);
    }

    private static async Task<(CompleteStepRequest? Request, bool Json, Error? Problem)> Parse(string[] args, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        var options = Options([.. args.Where(static a => a != "--json")]);
        if (options.IsError)
        {
            return (null, json, options.Error);
        }

        var (instance, flags) = options.Value;
        var fields = Fields(flags);
        if (fields.IsError)
        {
            return (null, json, fields.Error);
        }

        var outputs = new Dictionary<FactKey, ReadOnlyMemory<byte>>();
        foreach (var value in flags.Where(static o => o.Flag == "--output").Select(static o => o.Value))
        {
            var read = await Output(value, ct).ConfigureAwait(false);
            if (read.IsError)
            {
                return (null, json, read.Error);
            }

            outputs[read.Value.Key] = read.Value.Content;
        }

        var (run, outcome, nonce) = fields.Value;
        return (new CompleteStepRequest(instance, run, outcome, outputs, nonce), json, null);
    }

    /// <summary>The address and the '--flag value' pairs, each flag one this verb takes.</summary>
    private static Result<(string Instance, List<(string Flag, string Value)> Flags)> Options(string[] args)
    {
        if (args is not [{ Length: > 0 } instance, .. var pairs] || instance.StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"the first argument is the instance: its address ('{{item}}/{{recipe}}/{{attempt}}') or a bare item ({Usage})");
        }

        if (pairs.Length % 2 != 0)
        {
            return SindriErrors.InvalidInput.With($"'{pairs[^1]}' needs a value ({Usage})");
        }

        var flags = pairs.Chunk(2).Select(static p => (Flag: p[0], Value: p[1])).ToList();
        return flags.FirstOrDefault(static o => o.Flag is not ("--run" or "--outcome" or "--output" or "--nonce")) is { Flag: { } unknown }
            ? SindriErrors.InvalidInput.With($"'{unknown}' isn't an option ({Usage})")
            : (instance, flags);
    }

    /// <summary>'--run', '--outcome' and the optional '--nonce', each parsed.</summary>
    private static Result<(RunToken Run, OutcomeName Outcome, CommandNonce? Nonce)> Fields(List<(string Flag, string Value)> flags)
    {
        string? Single(string flag) => flags.LastOrDefault(o => o.Flag == flag).Value;
        if (Single("--run") is not { } run || !RunToken.TryFrom(run, out var token))
        {
            return SindriErrors.InvalidInput.With($"--run <token> is required, as status shows it ('{{entry}}.{{n}}') ({Usage})");
        }

        if (Single("--outcome") is not { } outcome || !OutcomeName.TryFrom(outcome, out var name))
        {
            return SindriErrors.InvalidInput.With($"--outcome <o> is required: one of the Step's declared outcomes ({Usage})");
        }

        if (Single("--nonce") is not { } given)
        {
            return (token, name, null);
        }

        return CommandNonce.TryFrom(given, out var nonce)
            ? (token, name, nonce)
            : SindriErrors.InvalidInput.With($"'{given}' isn't a nonce: 32 lower-case hex digits ({Usage})");
    }

    /// <summary>Reads '&lt;fact&gt;=&lt;file&gt;': the file's content, at most 1 MiB (spec §6.1); the server never sees the path.</summary>
    private static async Task<Result<(FactKey Key, ReadOnlyMemory<byte> Content)>> Output(string value, CancellationToken ct)
    {
        if (value.Split('=', 2) is not [var fact, { Length: > 0 } file] || !FactKey.TryFrom(fact, out var key))
        {
            return SindriErrors.InvalidInput.With($"'{value}' isn't <fact>=<file>, with a fact key such as fact:output ({Usage})");
        }

        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{file} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)");
            }

            return (key, await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable file is the caller's input error
        {
            return SindriErrors.InvalidInput.With($"{file} can't be read: {e.Message}");
        }
    }
}
