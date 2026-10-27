using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Host;

/// <summary>
/// Turns every request's caller headers into the call's <see cref="CallerContext"/> (API spec §6), queries included:
/// an unknown X-Sindri-Caller or a malformed X-Sindri-Repo or X-Sindri-Launch is 400 SND-API-007; a human without the per-start token, or
/// with a stale one, is 403 SND-AUTH-001, never downgraded to a session (a request carrying X-Sindri-Launch or
/// X-Sindri-Launch-Secret is a session before that check, whatever token it also carries: #1002); and the prover label from a human is honoured
/// only when this process's home is a temp or proof home, else 403 SND-AUTH-001. These refusals come before the command
/// chain, so they are logged, not audited (plan G13). The actor is composed from the class and the label, never taken
/// from a header.
/// </summary>
internal static partial class CallerContextMiddleware
{
    internal static WebApplication UseCallerContext(this WebApplication app)
    {
        _ = app.Use(Invoke);
        return app;
    }

    private static async Task Invoke(HttpContext context, RequestDelegate next)
    {
        var headers = context.Request.Headers;
        var parsed = CallerHeaderParsing.Parse(Header(headers, SindriHeaders.Caller), Header(headers, SindriHeaders.Terminal),
            Header(headers, SindriHeaders.Client), Header(headers, SindriHeaders.Repo), Header(headers, SindriHeaders.Launch));
        var log = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CallerContextMiddleware));
        var (method, path) = (context.Request.Method, context.Request.Path.Value ?? string.Empty);
        if (parsed.IsError)
        {
            LogUnknownCaller(log, method, path, parsed.Error.Message);
            await ApiProblems.Write(context, parsed.Error, StatusCodes.Status400BadRequest).ConfigureAwait(false);
            return;
        }

        var caller = AsLaunched(parsed.Value, headers.ContainsKey(SindriHeaders.LaunchSecret));
        if (caller.UnknownClient is { } label)
        {
            LogUnknownClient(log, method, path, label);
        }

        if (Refusal(context.RequestServices, caller, Header(headers, SindriHeaders.Token)) is { } refusal)
        {
            LogRefusedHuman(log, method, path, refusal);
            await ApiProblems.Write(context, SindriErrors.AuthTokenRequired.Code, StatusCodes.Status403Forbidden, refusal).ConfigureAwait(false);
            return;
        }

        if (caller.Launch is { } launch && !context.RequestServices.GetRequiredService<ILaunchFiles>().Launches().Contains(launch))
        {
            LogUnknownLaunch(log, method, path);
            await ApiProblems.Write(context, SindriErrors.AuthLaunchSecret.Code, StatusCodes.Status403Forbidden,
                "X-Sindri-Launch doesn't name an open launch (fix: call from the session Sindri launched, or leave the header out)").ConfigureAwait(false);
            return;
        }

        var accessor = context.RequestServices.GetRequiredService<ICallerContextAccessor>();
        var ids = context.RequestServices.GetRequiredService<IIdGenerator>();
        using (accessor.Enter(CallerContext.Http(new HttpCaller(caller.Class, caller.Terminal, caller.Client, caller.Repo, caller.Launch), ids)))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    /// <summary>A request carrying a launch secret comes from a session Sindri launched: it is a session whatever X-Sindri-Caller and X-Sindri-Token say (#1002).</summary>
    internal static ParsedCaller AsLaunched(ParsedCaller caller, bool carriesLaunchSecret) =>
        carriesLaunchSecret && caller.Class == CallerClass.Human ? caller with { Class = CallerClass.Session, Terminal = false } : caller;

    /// <summary>Why a human caller is refused (the SND-AUTH-001 detail), or null when it may go on.</summary>
    private static string? Refusal(IServiceProvider services, ParsedCaller caller, string? presented)
    {
        if (caller.Class != CallerClass.Human)
        {
            return null;
        }

        if (services.GetRequiredService<HeldHumanToken>().Current is not { } token || !token.Matches(presented))
        {
            return presented is null
                ? "A human caller needs this start's token in X-Sindri-Token (fix: restart the app or rerun the command)"
                : "X-Sindri-Token isn't this start's token (fix: restart the app or rerun the command)";
        }

        return caller.Client == ClientLabel.Prover && !services.GetRequiredService<ProverLabel>().Allowed
            ? "The prover label is honoured only for a temp or proof home (fix: run scripts/sindri-prove.sh against a home under $TMPDIR or a proof folder)"
            : null;
    }

    private static string? Header(IHeaderDictionary headers, string name) =>
        headers.TryGetValue(name, out var values) ? values.ToString() : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-API-007 {Method} {Path} refused: {Reason}")]
    private static partial void LogUnknownCaller(ILogger logger, string method, string path, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Path}: X-Sindri-Client '{Client}' isn't a label Sindri knows; read as no label")]
    private static partial void LogUnknownClient(ILogger logger, string method, string path, string client);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-AUTH-003 {Method} {Path} refused: X-Sindri-Launch names no open launch")]
    private static partial void LogUnknownLaunch(ILogger logger, string method, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-AUTH-001 {Method} {Path} refused: {Reason}")]
    private static partial void LogRefusedHuman(ILogger logger, string method, string path, string reason);
}
