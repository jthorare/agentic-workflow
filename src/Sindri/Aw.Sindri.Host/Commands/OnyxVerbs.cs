using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri onyx up' and 'sindri onyx down': they run Docker and write Onyx's files under the Sindri home, so any caller may run them (owner decision q93). Local: they run in this process and need no control plane. Progress is on standard error (lines and dots, through IOnyxProgress); standard output carries only the summary.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="approve">Runs `profile approve [hash]` the way the CLI does (preview without a hash, the typed challenge with one), for the approval `onyx up` shows inline; null skips the offer.</param>
/// <param name="purge">Runs `onyx down --purge` (the OnyxPurge stub, which does not depend on this class); null where the verb is not offered.</param>
internal sealed class OnyxVerbs(IServiceProvider services, Output output, Func<string?, Output, CancellationToken, Task<int>>? approve = null, Func<CancellationToken, Task<int>>? purge = null)
{
    /// <summary>'sindri onyx down --purge': the self-contained stub in OnyxPurge/, which shares nothing with the rest of this class.</summary>
    /// <param name="ct">Cancels the verb.</param>
    /// <returns>The stub's exit code; 2 when this host wasn't given one.</returns>
    internal async Task<int> Purge(CancellationToken ct) => purge is null ? await output.Error(SindriErrors.UnknownVerb.With("'onyx down --purge' isn't available here")).ConfigureAwait(false) : await purge(ct).ConfigureAwait(false);

    internal async Task<int> Up(bool json, CancellationToken ct)
    {
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

    /// <summary>'sindri onyx open': the sign-up page while no account was made through `onyx up`, else the connectors page.</summary>
    /// <param name="ct">Cancels the verb.</param>
    /// <returns>0 opened (or the address printed), 1 refused or not set up.</returns>
    internal async Task<int> Open(CancellationToken ct)
    {
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
}
