using System.Text.RegularExpressions;
using Npgsql;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>A Postgres error as text that is safe to record: the SQLSTATE and the message, without the values the server quotes in it.</summary>
internal static partial class ServerMessage
{
    private const string Placeholder = "\"…\"";

    /// <summary>The SQLSTATE and the message text. Outside class 42 (syntax errors and missing names, which quote the script's own identifiers) everything from the first quote character to the last (a double quote or a localized quote pair) becomes a placeholder, because the server does not escape a quote inside the value it quotes, so any shorter match would leak the tail: a data error such as invalid input syntax for type uuid quotes the offending value, which may be a secret.</summary>
    /// <param name="exception">The server's error.</param>
    /// <returns>The text.</returns>
    internal static string Of(PostgresException exception)
    {
        var text = exception.SqlState.StartsWith("42", StringComparison.Ordinal) ? exception.MessageText : Quoted.Replace(exception.MessageText, Placeholder);
        return exception.SqlState + " " + text;
    }

    [GeneratedRegex("[\"\u00ab\u00bb\u201c\u201d\u201e][\\s\\S]*[\"\u00ab\u00bb\u201c\u201d\u201e]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Quoted { get; }
}
