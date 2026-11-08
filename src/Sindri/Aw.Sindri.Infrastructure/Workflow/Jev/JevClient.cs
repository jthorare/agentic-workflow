#pragma warning disable MA0182 // why: red stub; the green commit registers it
using Aw.Kernel;
using Aw.Redaction;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow.Jev;

/// <summary>Jev over HTTP (POST /v1/systemone). Redacts what it sends, caps it at 160,000 characters and reports failures with the judge's reason codes.</summary>
/// <param name="http">Sends the request.</param>
/// <param name="readKey">Reads the TypeSafe API key (the environment, else the judge's keychain entry); null when there is none.</param>
/// <param name="redactor">Strips secrets before the text leaves the machine.</param>
internal sealed class JevClient(HttpClient http, Func<CancellationToken, ValueTask<string?>> readKey, IRedactor redactor) : IJevPort
{
    /// <inheritdoc />
    public ValueTask<Result<JevOutcome>> Ask(JevCall call, CancellationToken cancellationToken) =>
        throw new NotSupportedException(http.GetType().Name + readKey.GetType().Name + redactor.GetType().Name);
}
