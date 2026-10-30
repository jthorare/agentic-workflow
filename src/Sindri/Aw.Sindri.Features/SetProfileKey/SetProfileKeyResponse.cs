namespace Aw.Sindri.Features.SetProfileKey;

/// <summary>What 'profile set' did.</summary>
/// <param name="Path">The profile file.</param>
/// <param name="Key">The dotted key.</param>
/// <param name="Value">The value as typed.</param>
/// <param name="Changed">False when the key already had that value and the file was left alone.</param>
public sealed record SetProfileKeyResponse(string Path, string Key, string Value, bool Changed);
