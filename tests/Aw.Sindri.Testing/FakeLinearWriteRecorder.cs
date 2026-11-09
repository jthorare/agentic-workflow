using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearWriteRecorder"/>: keeps each recorded write, and can fail the next one.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearWriteRecorder is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearWriteRecorder : ILinearWriteRecorder
{
    private readonly List<(string Type, string Team, string? Issue, string? ClientId, string? Fields)> _recorded = [];

    /// <summary>Gets the writes recorded, in order.</summary>
    public IReadOnlyList<(string Type, string Team, string? Issue, string? ClientId, string? Fields)> Recorded => _recorded;

    /// <summary>Gets or sets the error the next records answer with; null records them.</summary>
    public Error? Failing { get; set; }

    /// <inheritdoc/>
    public ValueTask<Error?> Record(string type, string team, string? issue, string? clientId, string? fields, CancellationToken cancellationToken)
    {
        if (Failing is null)
        {
            _recorded.Add((type, team, issue, clientId, fields));
        }

        return ValueTask.FromResult(Failing);
    }
}
