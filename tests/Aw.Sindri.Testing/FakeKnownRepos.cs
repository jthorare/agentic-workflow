using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The account's recent repos, set by the test: the slugs it answers, or the error it fails with.</summary>
public sealed class FakeKnownRepos : IKnownRepos
{
    /// <summary>Gets or sets the answer; no slugs by default.</summary>
    public Result<IReadOnlySet<string>> Answer { get; set; } = Results.Success<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Gets how many reads were made.</summary>
    public int Reads { get; private set; }

    /// <summary>Answers with these slugs.</summary>
    /// <param name="slugs">'owner/name', lower case.</param>
    /// <returns>This fake.</returns>
    public FakeKnownRepos With(params string[] slugs)
    {
        Answer = Results.Success<IReadOnlySet<string>>(new HashSet<string>(slugs, StringComparer.Ordinal));
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlySet<string>>> Slugs(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(Answer);
    }
}
