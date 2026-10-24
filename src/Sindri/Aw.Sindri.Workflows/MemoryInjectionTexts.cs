using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The in-memory <see cref="IInjectionTexts"/> (plan: no text is stored): bounded at
/// <see cref="MaxBytes"/> bytes of text, dropping the least recently used past it; a newer run of an instance drops
/// the older runs' entries. Never persisted, never logged.
/// </summary>
internal sealed class MemoryInjectionTexts : IInjectionTexts
{
    /// <summary>The most text kept, in UTF-8 bytes; the entries are bounded by this alone (owner q93, 2026-10-23).</summary>
    internal const int MaxBytes = 64 * 1024 * 1024;

    private readonly Lock _gate = new();
    private readonly LinkedList<(InjectionKey Key, FilledTexts Texts, int Bytes)> _entries = new();
    private int _bytes;

    /// <inheritdoc/>
    public void Put(InjectionKey key, FilledTexts texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        lock (_gate)
        {
            Remove(e => e.Key.Equals(key) || (string.Equals(e.Key.Address, key.Address, StringComparison.Ordinal) && !string.Equals(e.Key.Run, key.Run, StringComparison.Ordinal)));
            var size = Size(texts);
            _ = _entries.AddFirst((key, texts, size));
            _bytes += size;
            while (_entries.Count > 1 && _bytes > MaxBytes)
            {
                _bytes -= _entries.Last!.Value.Bytes;   // why: the loop runs only while the list holds more than one entry
                _entries.RemoveLast();
            }
        }
    }

    /// <inheritdoc/>
    public FilledTexts? Get(InjectionKey key)
    {
        lock (_gate)
        {
            for (var node = _entries.First; node is not null; node = node.Next)
            {
                if (node.Value.Key.Equals(key))
                {
                    _entries.Remove(node);
                    _entries.AddFirst(node);
                    return node.Value.Texts;
                }
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public void Drop(string address)
    {
        lock (_gate)
        {
            Remove(e => string.Equals(e.Key.Address, address, StringComparison.Ordinal));
        }
    }

    private void Remove(Func<(InjectionKey Key, FilledTexts Texts, int Bytes), bool> match)
    {
        for (var node = _entries.First; node is not null;)
        {
            var next = node.Next;
            if (match(node.Value))
            {
                _bytes -= node.Value.Bytes;
                _entries.Remove(node);
            }

            node = next;
        }
    }

    internal static int Size(FilledTexts texts) =>
        texts.Sections.Sum(static s => System.Text.Encoding.UTF8.GetByteCount(s.Value)) + (texts.Result is { } result ? System.Text.Encoding.UTF8.GetByteCount(result.GetRawText()) : 0);
}
