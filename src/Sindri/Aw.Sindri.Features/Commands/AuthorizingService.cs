using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's authorization (API spec §2.1, §4.2): classifies the attempt (fail-closed, UI spec E-12) and checks its
/// class against the caller before validation, so an unauthorized caller learns nothing about valid input. A refusal is
/// SND-AUTH-002 naming the command and its CLI; the audit around it writes the only row.
/// </summary>
/// <param name="inner">Validation and the handler.</param>
/// <param name="callers">The caller of this attempt.</param>
/// <param name="catalog">The command catalog.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AuthorizingService<TRequest, TResponse>(IService<TRequest, TResponse> inner, ICallerContextAccessor callers, ICommandCatalog catalog)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>The service it wraps (for the chain-order test).</summary>
    internal IService<TRequest, TResponse> Next => Inner;

    public override ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, request);
        return Authorization.Decide(row.Class, callers.Current, row).Refusal is { } refusal
            ? ValueTask.FromResult<Result<TResponse>>(Named(request, row, refusal))
            : Inner.Handle(request, cancellationToken);
    }

    /// <summary>A row shared by several commands (the challenge row: approve and revoke) refuses with the one command the request asked for.</summary>
    private static Error Named(TRequest request, CommandRow row, Error refusal) =>
        request is IRefusalCli { RefusalCli: { } cli }
            ? refusal with { Message = refusal.Message.Replace(row.Cli.Text, cli, StringComparison.Ordinal) }
            : refusal;
}
