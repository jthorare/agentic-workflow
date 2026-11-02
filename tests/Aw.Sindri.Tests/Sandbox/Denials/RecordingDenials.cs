using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>An <see cref="ISandboxDenials"/> that keeps what it was sent; <see cref="Refuse"/> makes it answer with the ledger's refusal.</summary>
internal sealed class RecordingDenials : ISandboxDenials
{
    public List<SandboxDenied> Sent { get; } = [];

    public bool Refuse { get; set; }

    public ValueTask<Result<Unit>> Append(SandboxDenied denied, CancellationToken cancellationToken)
    {
        if (Refuse)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.NotRunning.With("refused"));
        }

        Sent.Add(denied);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
