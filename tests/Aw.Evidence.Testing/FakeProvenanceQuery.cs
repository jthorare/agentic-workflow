using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IProvenanceQuery over a list of profile emails; LIKE supports '%' only.</summary>
public sealed class FakeProvenanceQuery : IProvenanceQuery
{
    /// <summary>The profile emails.</summary>
    public IList<string> Emails { get; } = [];

    /// <summary>When set, every query fails with this message.</summary>
    public string? Failure { get; set; }

    /// <summary>The patterns queried.</summary>
    public IList<string> Patterns { get; } = [];

    /// <summary>The database each query named (null for the default), in order.</summary>
    public IList<string?> Databases { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> EmailsLikeAsync(string likePattern, string? database, CancellationToken cancellationToken)
    {
        Patterns.Add(likePattern);
        Databases.Add(database);
        return Answer([.. Emails.Where(e => Like(e, likePattern))]);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> AllEmailsAsync(string? database, CancellationToken cancellationToken)
    {
        Databases.Add(database);
        return Answer([.. Emails]);
    }

    private ValueTask<Result<IReadOnlyList<string>>> Answer(IReadOnlyList<string> rows) =>
        ValueTask.FromResult(Failure is null ? Results.Success(rows) : EvidenceErrors.ProbeFailed.With(Failure));

    private static bool Like(string value, string pattern)
    {
        var parts = pattern.Split('%');
        if (!value.StartsWith(parts[0], StringComparison.Ordinal) || !value.EndsWith(parts[^1], StringComparison.Ordinal))
        {
            return false;
        }

        return parts.Length > 1 || value.Length == pattern.Length;
    }
}
