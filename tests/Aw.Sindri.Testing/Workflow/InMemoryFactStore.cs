using System.Collections.Concurrent;
using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IFactStore"/> and <see cref="IFactContents"/>: content by its SHA-256.</summary>
public sealed class InMemoryFactStore : IFactStore, IFactContents
{
    private readonly ConcurrentDictionary<string, byte[]> _facts = new(StringComparer.Ordinal);

    /// <summary>The stored content, by SHA-256.</summary>
    public IReadOnlyDictionary<string, byte[]> Stored => _facts;

    /// <inheritdoc/>
    public ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(content.Span));
        _ = _facts.TryAdd(sha, content.ToArray());
        return ValueTask.FromResult<Result<FactValue>>(new FactValue("fact:sha256:" + sha, sha, HeadSha: null));
    }

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return ValueTask.FromResult<Result<ReadOnlyMemory<byte>>>(_facts.TryGetValue(fact.Sha256, out var content)
            ? (ReadOnlyMemory<byte>)content
            : SindriErrors.ApiInternal.With($"no fact {fact.Sha256} in this test"));
    }
}
