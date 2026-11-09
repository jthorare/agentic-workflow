using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri connection signin &lt;name&gt;': the stored connection signs in again. It prints the consent URL as soon as it has it, then waits for the callback.</summary>
internal sealed partial class ConnectionVerbs
{
    private async Task<int> SignIn(Parsed command, CancellationToken ct)
    {
        var started = await _client.Post<SignInConnectionRequest, AddConnectionResponse>(Route(command.Name) + "/signin", body: null, ct).ConfigureAwait(false);
        if (started.IsError)
        {
            return await _output.Error(started.Error).ConfigureAwait(false);
        }

        var response = started.Value.Value;
        if (response.ConsentUrl is null)
        {
            return command.Json
                ? await _output.Raw(started.Value.Body, 0).ConfigureAwait(false)
                : await _output.Text($"{response.Connection.Name} ({response.Connection.Kind}) is {response.Connection.Status}.\n", 0).ConfigureAwait(false);
        }

        await _output.Notice("Sign in at this address if your browser did not open:").ConfigureAwait(false);
        await _output.Notice(response.ConsentUrl).ConfigureAwait(false);
        var finished = await WaitForNewSignIn(command.Name, response.Connection.Config.GetValueOrDefault(SignedInAt), ct).ConfigureAwait(false);
        if (finished.IsError)
        {
            return await _output.Error(finished.Error).ConfigureAwait(false);
        }

        var view = finished.Value.Value.Connection;
        return command.Json
            ? await _output.Raw(finished.Value.Body, 0).ConfigureAwait(false)
            : await _output.Text($"Signed in {view.Name} ({view.Kind}), {view.Status}.\n", 0).ConfigureAwait(false);
    }

    // The Config key a finished sign-in stamps (OAuthSignInFlow.SignedInAt); the host project reads it by name.
    private const string SignedInAt = "signedInAt";

    // The row exists already, so "found" proves nothing, and a refresh tick moves LastRefreshedAt too: a finished sign-in leaves it connected with a different 'signedInAt'. A failed one is the API's own error (GET reports it).
    private async Task<Result<ApiAnswer<GetConnectionResponse>>> WaitForNewSignIn(string name, string? before, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(_signInWait);
        try
        {
            while (true)
            {
                var read = await _client.Get<GetConnectionResponse>(Route(name), limit.Token).ConfigureAwait(false);
                if (read.IsSuccess && read.Value.Value.LastSignInFailure is { } failed)
                {
                    // why: a connected row keeps its status and signedInAt when the sign-in again fails, so without this the wait ran out the 5 minutes
                    return SignInFailed(failed.Code);
                }

                if (read.IsError || (read.Value.Value.Connection is { } view && string.Equals(view.Status, "connected", StringComparison.Ordinal) && view.Config.GetValueOrDefault(SignedInAt) is { } at && !string.Equals(at, before, StringComparison.Ordinal)))
                {
                    return read;
                }

                await Task.Delay(_pollEvery, limit.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return SindriErrors.ConnectionSignInTimeout.With("no sign-in finished in 5 minutes (consent denied or the page was closed); run the command again");
        }
    }

    // The codes a sign-in ends with (OAuthSignInFlow); GET reports only the code, so the kind and title come from here.
    private static Error SignInFailed(string code)
    {
        ErrorDefinition[] known = [SindriErrors.ConnectionConsentDenied, SindriErrors.ConnectionStateMismatch, SindriErrors.ConnectionSignInTimeout, SindriErrors.ConnectionSecretUnavailable];
        var definition = Array.Find(known, d => string.Equals(d.Code, code, StringComparison.Ordinal));
        return definition is not null
            ? definition.With("the sign-in failed; the connection still works on its old sign-in")
            : new Error(code, ErrorKind.Conflict, "the sign-in failed; the connection still works on its old sign-in");
    }
}
