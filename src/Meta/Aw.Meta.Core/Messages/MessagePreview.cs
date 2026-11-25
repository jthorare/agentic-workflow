namespace Aw.Meta.Core.Messages;

/// <summary>The short form of a message body that a repeat surface prints; the full body is printed once, at delivery.</summary>
public static class MessagePreview
{
    private const int Length = 80;

    /// <summary>Cuts a body to its first 80 characters plus "..." when it is longer; a shorter body comes back unchanged.</summary>
    /// <param name="body">The message body.</param>
    /// <returns>The preview.</returns>
    public static string Of(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.Length > Length ? body[..Length] + "..." : body;
    }
}
