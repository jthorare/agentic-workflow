using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAttach;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri onyx up', 'sindri onyx down' and 'sindri onyx attach': they run Docker and write Onyx's files under the Sindri home (attach writes Sindri's key and profile section), so only a human at a terminal runs them (an agent
/// session or a non-terminal gets SND-AUTH-002, exit 1, like 'mcp register --apply'). Local: they run in this process, need no control plane, and change
/// nothing for a refused caller. Progress is on standard error (lines and dots, through IOnyxProgress); standard output carries only the summary.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="session">Who runs the CLI.</param>
/// <param name="approve">Runs `profile approve [hash]` the way the CLI does (preview without a hash, the typed challenge with one), for the approval `onyx up` shows inline; null skips the offer.</param>
internal sealed class OnyxVerbs(IServiceProvider services, Output output, AgentSession session, Func<string?, Output, CancellationToken, Task<int>>? approve = null)
{
    internal async Task<int> Up(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("up", "runs Docker and writes Onyx's files").ConfigureAwait(false);
        }

        Result<OnyxUpResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxUpRequest, OnyxUpResponse>>().Handle(new OnyxUpRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The runner killed the docker child, the services released the lock and removed their temporary files on the way out; each step is recorded only once done.
            return Interrupted("interrupted; run `sindri onyx up` again to resume");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        try
        {
            return await new OnyxHandOver(services, output, approve).Finish(answer.Value, json, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted; run `sindri onyx up` again to resume");
        }
    }

    /// <summary>'sindri onyx attach &lt;url&gt;': the Onyx at a cluster address instead of the stack `up` runs, then the same account stage and hand-over. The human's verb like `up`.</summary>
    /// <param name="url">Onyx's base URL.</param>
    /// <param name="json">Print one JSON document.</param>
    /// <param name="ct">Cancels the verb.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> Attach(string url, bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("attach", "writes Sindri's Onyx key and the profile's Onyx section").ConfigureAwait(false);
        }

        try
        {
            var answer = await services.GetRequiredService<IService<OnyxAttachRequest, OnyxAccountStageResult>>().Handle(new OnyxAttachRequest(url), ct).ConfigureAwait(false);
            return answer.IsError
                ? await Fail(output, answer.Error).ConfigureAwait(false)
                : await new OnyxHandOver(services, output, approve).FinishAttach(answer.Value, json, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted; run `sindri onyx attach` again to resume");
        }
    }

    /// <summary>'sindri onyx open': the sign-up page while no account was made through `onyx up`, else the connectors page.</summary>
    /// <param name="ct">Cancels the verb.</param>
    /// <returns>0 opened (or the address printed), 1 refused or not set up.</returns>
    internal async Task<int> Open(CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("open", "opens a page in the browser").ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        var state = services.GetRequiredService<IOnyxWorkspace>().ReadState();
        if (state is null)
        {
            _ = await output.Error(SindriErrors.InvalidInput.With("Onyx isn't set up on this box (fix: run `sindri onyx up`)")).ConfigureAwait(false);
            return 1;
        }

        var page = new Uri($"http://127.0.0.1:{state.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}" + (state.Steps.ContainsKey(OnyxSteps.Signup) ? "/admin/add-connector" : "/auth/signup"));
        var opened = services.GetRequiredService<IBrowser>().Open(page);
        return await output.Text(opened ? $"Opening {page}\n" : $"Open {page} in your browser.\n", 0).ConfigureAwait(false);
    }

    internal async Task<int> Down(bool json, CancellationToken ct)
    {
        if (!session.Human)
        {
            return await Refused("down", "stops Docker containers").ConfigureAwait(false);
        }

        Result<OnyxDownResponse> answer;
        try
        {
            answer = await services.GetRequiredService<IService<OnyxDownRequest, OnyxDownResponse>>().Handle(new OnyxDownRequest(), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Interrupted("interrupted; run `sindri onyx down` again to finish stopping Onyx");
        }

        if (answer.IsError)
        {
            return await Fail(output, answer.Error).ConfigureAwait(false);
        }

        var progress = services.GetRequiredService<IOnyxProgress>();
        foreach (var warning in answer.Value.Warnings)
        {
            progress.Line(warning);
        }

        return json
            ? await output.Json(answer.Value, 0).ConfigureAwait(false)
            : await output.Text(answer.Value.WasRunning
                ? $"Onyx stopped: {string.Join(", ", answer.Value.Stopped)} (its data and images are kept; `sindri onyx up` starts it again).\n"
                : "Onyx is stopped (nothing was running).\n", 0).ConfigureAwait(false);
    }

    /// <summary>Prints an error the way the onyx verbs exit: 1 for an SND-ONYX error or a busy command (attention: the message names the fix), 2 for anything else.</summary>
    /// <param name="output">Where the error goes.</param>
    /// <param name="error">The error.</param>
    /// <returns>The exit code.</returns>
    internal static async Task<int> Fail(Output output, Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return error.Code.StartsWith("SND-ONYX-", StringComparison.Ordinal) || error.Code == SindriErrors.CommandBusy.Code ? 1 : 2;
    }

    internal static string Steps(IReadOnlyList<string> steps) => steps.Count == 0 ? "none" : string.Join(", ", steps);

    // Through the progress writer, which ends an open row of dots before the line, so the resume line never starts mid-row.
    private int Interrupted(string text)
    {
        services.GetRequiredService<IOnyxProgress>().Line(text);
        return Output.InterruptedExit;
    }

    // The printed refusal: SND-AUTH-002, exit 1 (attention, not a tool error), with nothing called.
    private async Task<int> Refused(string verb, string what)
    {
        _ = await output.Error(SindriErrors.AuthRefused.With(
            $"`sindri onyx {verb}` {what}, so only a human at a terminal runs it (this CLI ran as a session: {session.SessionReason}; fix: ask the human to run `sindri onyx {verb}`)")).ConfigureAwait(false);
        return 1;
    }
}
