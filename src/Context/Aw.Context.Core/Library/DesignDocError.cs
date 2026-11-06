namespace Aw.Context.Core.Library;

/// <summary>One finding about a design doc: a stable <c language="text">SND-LIB-nnn</c> code, the line, what is wrong and how to fix it.</summary>
/// <param name="Code">The code (<see cref="DesignDocCodes"/>).</param>
/// <param name="Line">The line it is on; null for a whole-file error (too large).</param>
/// <param name="Message">What is wrong; names the file and the line.</param>
/// <param name="Fix">What to change.</param>
public sealed record DesignDocError(string Code, int? Line, string Message, string Fix);
