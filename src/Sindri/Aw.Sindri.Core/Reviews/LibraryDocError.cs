namespace Aw.Sindri.Core.Reviews;

/// <summary>One finding about a design doc, as the library page shows it (PR review page spec §6.1).</summary>
/// <param name="Rule">The <c language="text">SND-LIB-nnn</c> code.</param>
/// <param name="Text">What is wrong, naming the file and line, then how to fix it.</param>
public sealed record LibraryDocError(string Rule, string Text);
