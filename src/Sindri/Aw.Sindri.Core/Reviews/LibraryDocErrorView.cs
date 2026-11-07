namespace Aw.Sindri.Core.Reviews;

/// <summary>One finding about a doc.</summary>
/// <param name="Rule">The <c language="text">SND-LIB-nnn</c> code.</param>
/// <param name="Text">The finding: where it is, what is wrong, how to fix it.</param>
public sealed record LibraryDocErrorView(string Rule, string Text);
