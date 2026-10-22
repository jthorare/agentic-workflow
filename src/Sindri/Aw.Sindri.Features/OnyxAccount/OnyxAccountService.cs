using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 6-9. (6) The owner makes their own account in Onyx's sign-up page (Onyx makes the first user the admin; Sindri doesn't register that account). (7, 8) With that login, asked for once at the terminal and forgotten, Sindri makes its
/// dedicated admin user and its token and stores the token (<see cref="OnyxKeyMinter"/>). (9) The profile section is written. Each step is recorded in state.json once done, so a rerun skips it. Without a terminal nothing is asked: the stage ends "needs you".
/// </summary>
internal sealed class OnyxAccountService(IOnyxWorkspace workspace, IOnyxPrompts prompts, IBrowser browser, OnyxKeyMinter minter, IOnyxProfileWriter profile, IOnyxProgress progress)
    : IService<OnyxAccountRequest, OnyxAccountResponse>
{
    public ValueTask<Result<OnyxAccountResponse>> Handle(OnyxAccountRequest request, CancellationToken cancellationToken) => Handle(request, login: null, cancellationToken);

    /// <summary>Steps 6-9 with the login the app's form gave: nothing is asked at a terminal, the sign-up is taken as done (the owner says so by sending the login) and the login is used for the key and forgotten.</summary>
    /// <param name="request">Onyx's port and release.</param>
    /// <param name="login">The owner's Onyx admin login, or null to ask at the terminal as `sindri onyx up` does.</param>
    /// <param name="cancellationToken">Cancels the waiting and the requests.</param>
    /// <returns>What the steps did, or the error that stopped them.</returns>
    internal async ValueTask<Result<OnyxAccountResponse>> Handle(OnyxAccountRequest request, OnyxLogin? login, CancellationToken cancellationToken)
    {
        // The same workspace lock `up` and `down` take: a second `up` or a `down` can't run while this one waits for the owner at a prompt.
        var lockAttempt = workspace.TryLock();
        if (lockAttempt.IsError)
        {
            return lockAttempt.Error;
        }

        using var held = lockAttempt.Value;
        if (held is null)
        {
            return SindriErrors.CommandBusy.With("another `sindri onyx` command is running; wait for it to finish, then run this again");
        }

        return await Run(request, login, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<OnyxAccountResponse>> Run(OnyxAccountRequest request, OnyxLogin? login, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{request.Port}");
        var state = workspace.ReadState() ?? new OnyxSetupState(request.Port, new Dictionary<string, string>(StringComparer.Ordinal));
        var steps = new Dictionary<string, string>(state.Steps, StringComparer.Ordinal);
        var pointer = state.KeyPointer;
        List<OnyxStepResult> results = [];

        var signup = await Signup(url, steps, login is not null, cancellationToken).ConfigureAwait(false);
        results.Add(new OnyxStepResult(OnyxSteps.Signup, signup));
        Save(state, request.Port, steps, pointer);
        if (signup == OnyxStepOutcome.NeedsHuman)
        {
            return new OnyxAccountResponse(url, pointer, ProfileChanged: false, results);
        }

        var key = await Key(request.Port, steps, pointer, login, cancellationToken).ConfigureAwait(false);
        if (key.IsError)
        {
            return key.Error;
        }

        (pointer, var keyOutcome) = key.Value;
        results.Add(new OnyxStepResult(OnyxSteps.Key, keyOutcome));
        Save(state, request.Port, steps, pointer);
        if (pointer is null)
        {
            return new OnyxAccountResponse(url, KeyPointer: null, ProfileChanged: false, results);   // the key step waits for the human
        }

        var written = profile.SetOnyx(url, pointer, request.Version);
        if (written.IsError)
        {
            return written.Error;
        }

        steps[OnyxSteps.Profile] = OnyxSteps.Stamp("profile", url, pointer, request.Version);
        Save(state, request.Port, steps, pointer);
        results.Add(new OnyxStepResult(OnyxSteps.Profile, written.Value ? OnyxStepOutcome.Ran : OnyxStepOutcome.Skipped));
        return new OnyxAccountResponse(url, pointer, written.Value, results);
    }

    private async ValueTask<OnyxStepOutcome> Signup(string url, Dictionary<string, string> steps, bool loginGiven, CancellationToken cancellationToken)
    {
        if (steps.ContainsKey(OnyxSteps.Signup))
        {
            progress.Line("signup: already done");
            return OnyxStepOutcome.Skipped;
        }

        if (loginGiven)
        {
            steps[OnyxSteps.Signup] = OnyxSteps.Stamp("signup");   // the login in hand is the owner saying they signed up
            return OnyxStepOutcome.Ran;
        }

        if (!prompts.Interactive)
        {
            progress.Line($"signup: needs you at a terminal; open {url}/auth/signup and create your Onyx account, then run `sindri onyx up` again");
            return OnyxStepOutcome.NeedsHuman;
        }

        var page = new Uri($"{url}/auth/signup");
        if (!browser.Open(page))
        {
            progress.Line($"open {page} in your browser");
        }

        await prompts.Pause("Create your Onyx account in the browser (the first account becomes the admin), then press Enter", cancellationToken).ConfigureAwait(false);
        steps[OnyxSteps.Signup] = OnyxSteps.Stamp("signup");
        return OnyxStepOutcome.Ran;
    }

    private async ValueTask<Result<(string? Pointer, OnyxStepOutcome Outcome)>> Key(int port, Dictionary<string, string> steps, string? pointer, OnyxLogin? login, CancellationToken cancellationToken)
    {
        if (steps.ContainsKey(OnyxSteps.Key))
        {
            if (await minter.Stands(port, pointer, cancellationToken).ConfigureAwait(false))
            {
                progress.Line("key: already done");
                return (pointer, OnyxStepOutcome.Skipped);
            }

            progress.Line($"key: the recorded key no longer works. {OnyxKeyMinter.LeftoverNote}");
        }

        if (login is null && !prompts.Interactive)
        {
            progress.Line("key: needs you at a terminal to ask for your Onyx login; run `sindri onyx up` there");
            return (null, OnyxStepOutcome.NeedsHuman);
        }

        var minted = login is null ? await minter.Mint(port, cancellationToken).ConfigureAwait(false) : await minter.Mint(port, login, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        steps[OnyxSteps.Key] = OnyxSteps.Stamp("key", minted.Value);
        return (minted.Value, OnyxStepOutcome.Ran);
    }

    private void Save(OnyxSetupState state, int port, Dictionary<string, string> steps, string? pointer)
        => workspace.WriteState(state with { Port = port, Steps = new Dictionary<string, string>(steps, StringComparer.Ordinal), KeyPointer = pointer });
}
