namespace Aw.Sindri.Host.Commands;

/// <summary>Where 'sindri onyx down --purge' asks for its typed yes and reads the answer.</summary>
/// <param name="Input">Where the answer is read.</param>
/// <param name="Question">Where the question is asked (standard error).</param>
/// <param name="Terminal">Whether standard input and output are a terminal; the yes is asked only there, so a piped "yes" deletes nothing.</param>
internal sealed record PurgePrompt(TextReader Input, TextWriter Question, bool Terminal);
