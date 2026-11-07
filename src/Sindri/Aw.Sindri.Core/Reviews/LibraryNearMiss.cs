namespace Aw.Sindri.Core.Reviews;

/// <summary>A file that almost reads as a design doc.</summary>
/// <param name="Path">The file, with the home folder as <c language="text">~</c>.</param>
/// <param name="Rule">The <c language="text">SND-LIB-nnn</c> code.</param>
/// <param name="Text">What is wrong, then how to fix it.</param>
public sealed record LibraryNearMiss(string Path, string Rule, string Text);
