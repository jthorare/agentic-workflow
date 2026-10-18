using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IUsageReader"/> with set figures: what was asked is kept, so a test can check the window.</summary>
/// <param name="name">The reader's name ('claude-code').</param>
/// <param name="verified">Whether the reader counts as checked against the real CLI.</param>
public sealed class FakeUsageReader(string name, bool verified = true) : IUsageReader
{
    /// <summary>What every read returns.</summary>
    public IReadOnlyList<ModelTokens> Used { get; set; } = [];

    /// <summary>The window of the last <see cref="Between"/>.</summary>
    public (DateTimeOffset From, DateTimeOffset To)? Asked { get; private set; }

    /// <summary>How many times <see cref="Between"/> ran.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public string Name { get; } = name;

    /// <inheritdoc/>
    public bool Verified { get; } = verified;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelTokens>> Between(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        Asked = (from, to);
        Reads++;
        return ValueTask.FromResult(Used);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelTokens>> OfSession(SessionId session, CancellationToken cancellationToken) => ValueTask.FromResult(Used);
}
