using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Prepare;

/// <summary>An <see cref="IWorkspacePrepare"/> that records what it was asked, or refuses with the error it is given.</summary>
public sealed class FakeWorkspacePrepare : IWorkspacePrepare
{
    private readonly List<(LaunchRequest Request, string WorkDirectory)> _prepared = [];

    /// <summary>The error Prepare returns, or null to succeed.</summary>
    public Error? Refuse { get; set; }

    /// <summary>Every call so far, in order.</summary>
    public IReadOnlyList<(LaunchRequest Request, string WorkDirectory)> Prepared => _prepared;

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Prepare(LaunchRequest request, string workDirectory, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        _prepared.Add((request, workDirectory));
        return ValueTask.FromResult(Refuse is { } error ? (Result<Unit>)error : Results.Success(Unit.Value));
    }
}
