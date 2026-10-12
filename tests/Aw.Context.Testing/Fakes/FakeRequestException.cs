using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>A request the fake refuses, with the status and a message that says what to change.</summary>
internal sealed class FakeRequestException(int status, string code, string message) : Exception(message)
{
    public FakeRequestException()
        : this(400, "BAD_REQUEST", "Bad request.")
    {
    }

    public FakeRequestException(string message)
        : this(400, "BAD_REQUEST", message)
    {
    }

    public FakeRequestException(string message, Exception innerException)
        : this(400, "BAD_REQUEST", message + " " + innerException.Message)
    {
    }

    public int Status { get; } = status;

    public string Code { get; } = code;

    /// <summary>A structured error body (Onyx's validation 'detail' list), or null for a plain message.</summary>
    public JsonNode? Detail { get; init; }

    public static FakeRequestException Invalid(string message) => new(400, "GRAPHQL_VALIDATION_FAILED", message);
}
