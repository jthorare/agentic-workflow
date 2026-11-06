using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Linear;

/// <summary>A mutation Linear would refuse inside a 200 answer: the GraphQL error it carries goes in 'errors', and the field's data is null.</summary>
internal sealed class LinearMutationException(JsonObject error) : Exception(error["message"]?.GetValue<string>())
{
    public LinearMutationException()
        : this(new JsonObject { ["message"] = "Mutation refused." })
    {
    }

    public LinearMutationException(string message)
        : this(new JsonObject { ["message"] = message })
    {
    }

    public LinearMutationException(string message, Exception innerException)
        : this(new JsonObject { ["message"] = message + " " + innerException.Message })
    {
    }

    public JsonObject Error { get; } = error;
}
