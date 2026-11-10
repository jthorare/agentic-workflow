namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The error types a refused answer carries, so the client can tell them apart.</summary>
internal static class AnswerRefusal
{
    /// <summary>No question waits at the node.</summary>
    public const string NotWaiting = "AnswerNotWaiting";

    /// <summary>The answer is not one the node takes.</summary>
    public const string BadAnswer = "AnswerNotAccepted";
}
