using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// ISignInSession on 127.0.0.1: listens on a free port, opens the consent URL, takes ONE callback (state checked, code exchanged), and closes
/// the listener when the callback lands, on denial, on timeout and on cancel.
/// </summary>
/// <param name="oauth">Exchanges the code.</param>
/// <param name="browser">Opens the consent URL.</param>
/// <param name="timeout">How long to wait for the callback (5 minutes in production).</param>
/// <param name="randomBytes">The PKCE byte source.</param>
internal sealed class LoopbackSignIn(IOAuthClient oauth, IBrowserOpener browser, TimeSpan timeout, Func<int, byte[]> randomBytes) : ISignInSession
{
    private const string CallbackPath = "/callback";

    private readonly IOAuthClient _oauth = oauth;
    private readonly IBrowserOpener _browser = browser;
    private readonly TimeSpan _timeout = timeout;
    private readonly Func<int, byte[]> _randomBytes = randomBytes;

    public ValueTask<Result<SignInStarted>> Start(ConnectionPreset preset, string clientId, IReadOnlyList<string> scopes, string? clientSecret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(scopes);
#pragma warning disable CA2000 // the listener's owner is Complete, which closes it on every way out
        var listener = Listen(out var port);
#pragma warning restore CA2000
        if (listener is null)
        {
            return ValueTask.FromResult<Result<SignInStarted>>(SindriErrors.ConnectionSecretUnavailable.With("no free loopback port to listen on for the sign-in callback"));
        }

        var redirect = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}{CallbackPath}";
        var pkce = PkceChallenge.Create(_randomBytes);
        var request = new AuthorizationRequest(preset, clientId, redirect, scopes.Count > 0 ? scopes : preset.DefaultScopes, pkce);
        var url = request.ToUrl();
        var completion = Complete(listener, new SignIn(preset, clientId, redirect, pkce, clientSecret), cancellationToken);
        _ = _browser.Open(url);
        return ValueTask.FromResult<Result<SignInStarted>>(new SignInStarted(url, completion));
    }

    public ValueTask<Result<SignInStarted>> StartMcp(Uri server, CancellationToken cancellationToken)
    {
        _ = (server, cancellationToken);
        throw new NotSupportedException();
    }

    private static HttpListener? Listen(out int port)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/");
            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException)
            {
                listener.Close();   // the port was taken between the probe and the start: try another
            }
        }

        port = 0;
        return null;
    }

    private async Task<Result<TokenSet>> Complete(HttpListener listener, SignIn sign, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_timeout);
        try
        {
            var callback = await WaitForCallback(listener, limit.Token).ConfigureAwait(false);
            return await Answer(callback, sign, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ConnectionSignInTimeout.With("the sign-in was cancelled");
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.ConnectionSignInTimeout.With("nobody finished the sign-in in " + _timeout.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture) + " minutes");
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            return SindriErrors.ConnectionSecretUnavailable.With("the sign-in listener failed (" + e.GetType().Name + ")");   // the completion must always end with a result, never a thrown task
        }
        finally
        {
            listener.Close();
        }
    }

    // The callback's state is checked before anything else, and the page says "signed in" only after the code was traded for tokens.
    private async Task<Result<TokenSet>> Answer(Callback callback, SignIn sign, CancellationToken cancellationToken)
    {
        if (!sign.Pkce.Verify(callback.Query["state"]))
        {
            await Reply(callback, 400, "Sindri: this sign-in did not match. Close this tab and run the command again.").ConfigureAwait(false);
            return SindriErrors.ConnectionStateMismatch.With("the callback's state is not the one this sign-in made");
        }

        if (callback.Query["error"] is { Length: > 0 })
        {
            await Reply(callback, 200, "Sindri: consent was denied. You can close this tab.").ConfigureAwait(false);
            return SindriErrors.ConnectionConsentDenied.With($"{sign.Preset.DisplayName} did not get consent");
        }

        if (callback.Query["code"] is not { Length: > 0 } code)
        {
            await Reply(callback, 400, "Sindri: the callback had no code.").ConfigureAwait(false);
            return SindriErrors.ConnectionConsentDenied.With($"{sign.Preset.DisplayName} sent the callback without a code");
        }

        var exchanged = await _oauth.ExchangeCode(new CodeExchange(sign.Preset, sign.Client, code, sign.Redirect, sign.Pkce.Verifier, sign.ClientSecret), cancellationToken).ConfigureAwait(false);
        await Reply(callback, exchanged.IsSuccess ? 200 : 502, exchanged.IsSuccess ? "Sindri: signed in. You can close this tab." : "Sindri: the sign-in did not finish. Go back to the terminal for the reason.").ConfigureAwait(false);
        return exchanged;
    }

    // Takes requests until one hits the callback path; anything else (a favicon) gets a 404.
    private static async Task<Callback> WaitForCallback(HttpListener listener, CancellationToken cancellationToken)
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (context.Request.Url is { } url && string.Equals(url.AbsolutePath, CallbackPath, StringComparison.Ordinal))
            {
                return new Callback(context, System.Web.HttpUtility.ParseQueryString(url.Query));
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }

    private static async Task Reply(Callback callback, int status, string text)
    {
        var bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset=utf-8><title>Sindri</title><p>" + WebUtility.HtmlEncode(text));
        try
        {
            callback.Context.Response.StatusCode = status;
            callback.Context.Response.ContentType = "text/html; charset=utf-8";
            await callback.Context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            callback.Context.Response.Close();
        }
        catch (Exception e) when (e is HttpListenerException or IOException or ObjectDisposedException)
        {
            // the browser went away before the page was written: the sign-in's own result is unaffected
        }
    }

    private sealed record SignIn(ConnectionPreset Preset, string Client, string Redirect, PkceChallenge Pkce, string? ClientSecret);

    private sealed record Callback(HttpListenerContext Context, System.Collections.Specialized.NameValueCollection Query);
}
