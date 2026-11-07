using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>The id Sindri mints for a new issue or comment and hands Linear as the object's own id (Linear's create inputs accept one). A retry after an unknown outcome sends the same one, so it can't create twice, and the re-read finds the object by it.</summary>
public sealed record LinearClientId
{
    private LinearClientId(string value)
    {
        Value = value;
    }

    /// <summary>The uuid in its 36-character lowercase form.</summary>
    public string Value { get; }

    /// <summary>Reads an id from text.</summary>
    /// <param name="text">A uuid in the 36-character form.</param>
    /// <returns>The id (lowercased), or SND-CONTEXT-006 for text that isn't a uuid or is the empty one.</returns>
    public static Result<LinearClientId> Parse(string? text) =>
        text is not null && text.Length == 36 && Guid.TryParseExact(text, "D", out var parsed) && parsed != Guid.Empty
            ? new LinearClientId(text.ToLowerInvariant())
            : ContextErrors.QueryInvalid.With($"'{text}' isn't a client id (a uuid)");

    /// <inheritdoc />
    public override string ToString() => Value;
}
