using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host.Onyx;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What `sindri onyx up` does once the stack is running (spec §4.1 steps 6-10): your account and Sindri's key (<see cref="OnyxAccountResponse"/>), the profile approval shown inline, the connectors page and the connector
/// checklist. Standard output carries only the summary; the questions go to standard error through the prompts.
/// </summary>
/// <param name="services">The CLI's scoped services.</param>
/// <param name="output">Where answers go.</param>
/// <param name="approve">Runs `profile approve [hash]` the way the CLI does, writing to the given output (standard error with --json, so standard output stays one document); null skips the approval offer.</param>
internal sealed class OnyxHandOver(IServiceProvider services, Output output, Func<string?, Output, CancellationToken, Task<int>>? approve)
{
    private const string ConnectorChecklist = "Connect your sources in Onyx (the logins and tokens are yours, and so is the company's sign-off):\n  Linear: the product's teams, with a start date that covers the backtests\n  Slack: the product's channels\n  GitHub: the product's repositories, issues on, with a read-only personal access token\nThen `sindri doctor` shows context:onyx and onyx:stack.";

    internal async Task<int> Finish(OnyxUpResponse up, bool json, CancellationToken ct)
    {
        if (up.NeedsHuman is { Count: > 0 } waiting)
        {
            return await Print(up, account: null, $"needs you: {string.Join(", ", waiting)}", code: 0, json).ConfigureAwait(false);   // a stack step waits for the human: the account stage comes after it
        }

        var account = await services.GetRequiredService<IService<OnyxAccountRequest, OnyxAccountResponse>>().Handle(new OnyxAccountRequest(up.Port, up.Version), ct).ConfigureAwait(false);
        if (account.IsError)
        {
            return await OnyxVerbs.Fail(output, account.Error).ConfigureAwait(false);
        }

        var (next, code) = await Tail(account.Value, json, ct).ConfigureAwait(false);
        return await Print(up, account.Value, next, code, json).ConfigureAwait(false);
    }

    private async Task<int> Print(OnyxUpResponse up, OnyxAccountResponse? account, string next, int code, bool json)
        => json
            ? await output.Json(new OnyxUpSummary(up, account), code).ConfigureAwait(false)
            : await output.Text($"{"url",-10}{up.Url}\n{"version",-10}{up.Version}\n{"ran",-10}{OnyxVerbs.Steps(up.Ran)}\n{"skipped",-10}{OnyxVerbs.Steps(up.Skipped)}\n{next}\n", code).ConfigureAwait(false);

    private async Task<(string Text, int ExitCode)> Tail(OnyxAccountResponse done, bool json, CancellationToken ct)
    {
        if (done.NeedsHuman)
        {
            var waiting = done.Steps.Where(static s => s.Outcome == OnyxStepOutcome.NeedsHuman).Select(static s => s.Step);
            return ($"needs you: {string.Join(", ", waiting)} (run `sindri onyx up` at a terminal)", 0);
        }

        var approved = approve is null || await ApproveInline(approve, json ? output.OnStandardError() : output, ct).ConfigureAwait(false);
        _ = services.GetRequiredService<IBrowser>().Open(new Uri(done.Url + "/admin/add-connector"));
        var key = $"{"key",-10}{done.KeyPointer} (a pointer; the key itself is in the store)\n";
        var profile = approved ? string.Empty : "The profile section is written but not approved yet: run `sindri profile approve`.\n";
        return (key + profile + ConnectorChecklist, approved ? 0 : 1);
    }

    // The preview (the hash and what changed); when it is not approved yet, the typed challenge, as `sindri profile approve <hash>` asks it.
    private async Task<bool> ApproveInline(Func<string?, Output, CancellationToken, Task<int>> approveVerb, Output where, CancellationToken ct)
    {
        var loaded = await services.GetRequiredService<IProfileSource>().Load(ct).ConfigureAwait(false);
        if (loaded.IsError)
        {
            _ = await output.Error(loaded.Error).ConfigureAwait(false);
            return false;
        }

        return await approveVerb(null, where, ct).ConfigureAwait(false) == 0
            || await approveVerb(loaded.Value.Hash.Value, where, ct).ConfigureAwait(false) == 0;
    }
}
