using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The fake <see cref="ICommitText"/>: the text it was given for a range, an empty text for any other, and every range it was asked for.</summary>
public sealed class FakeCommitText : ICommitText
{
    private readonly Dictionary<(string Base, string Head), string> _ranges = [];

    private readonly List<(string Checkout, string Base, string Head)> _asked = [];

    /// <summary>The ranges asked for, in order: checkout, base and head.</summary>
    public IReadOnlyList<(string Checkout, string Base, string Head)> Asked => _asked;

    /// <summary>Whether every call fails, as an unreadable checkout does.</summary>
    public bool Unreadable { get; set; }

    /// <summary>Holds the text of a range.</summary>
    /// <param name="baseSha">The base.</param>
    /// <param name="head">The head.</param>
    /// <param name="text">The messages and patches.</param>
    /// <returns>The same instance.</returns>
    public FakeCommitText With(string baseSha, string head, string text)
    {
        _ranges[(baseSha, head)] = text;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Range(string checkout, string baseSha, string head, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _asked.Add((checkout, baseSha, head));
        return ValueTask.FromResult<Result<string>>(Unreadable
            ? SindriErrors.InvalidInput.With("the commits can't be read")
            : _ranges.TryGetValue((baseSha, head), out var text) ? text : string.Empty);
    }
}
