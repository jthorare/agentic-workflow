namespace Aw.Sindri.Host.Commands;

/// <summary>One file that looks like a design doc but isn't read.</summary>
/// <param name="Path">The file.</param>
/// <param name="Rule">The code (SND-LIB-019).</param>
/// <param name="Text">What is wrong and the fix.</param>
internal sealed record LibraryNearMissRow(string Path, string Rule, string Text);
