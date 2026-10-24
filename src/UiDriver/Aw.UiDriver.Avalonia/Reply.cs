namespace Aw.UiDriver.Avalonia;

/// <summary>An answer: status, media type, body.</summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="ContentType">The media type.</param>
/// <param name="Body">The body.</param>
internal sealed record Reply(int Status, string ContentType, ReadOnlyMemory<byte> Body);
