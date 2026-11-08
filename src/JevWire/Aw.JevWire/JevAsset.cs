namespace Aw.JevWire;

/// <summary>One entry of a Jev request's 'assets': an image that is a part of the multipart body.</summary>
/// <param name="Type">Always 'image'.</param>
/// <param name="Part">The name of the multipart file part that holds it.</param>
public sealed record JevAsset(string Type, string Part);
