using Aw.Evidence.Infrastructure.Databases;
using Npgsql;

namespace Aw.Evidence.Tests.Infrastructure;

public sealed class ServerMessageTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("invalid input syntax for type uuid: \"ab\"cdefgh\"", "cdefgh")]
    [InlineData("invalid input syntax for type uuid: \"ab\"\"cdefgh\"", "cdefgh")]
    [InlineData("invalid input syntax for type uuid: \"ab\" and also \"zz-secret-tail\"", "zz-secret-tail")]
    [InlineData("ung\u00fcltige Eingabesyntax f\u00fcr Typ uuid: \u201ezz-secret-tail\u201c", "zz-secret-tail")]
    public void A_quoted_value_outside_class_42_is_hidden_whatever_it_contains(string message, string secret)
    {
        var text = ServerMessage.Of(new PostgresException(message, "ERROR", "ERROR", "22P02"));

        Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        Assert.StartsWith("22P02 ", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_class_42_message_keeps_the_scripts_own_identifiers()
    {
        var text = ServerMessage.Of(new PostgresException("relation \"notes\" does not exist", "ERROR", "ERROR", "42P01"));

        Assert.Equal("42P01 relation \"notes\" does not exist", text);
    }
}
