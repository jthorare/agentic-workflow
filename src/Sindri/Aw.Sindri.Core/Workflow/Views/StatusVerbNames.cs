namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The wire names of <see cref="StatusVerb"/>, for text output.</summary>
public static class StatusVerbNames
{
    /// <summary>The name the verb has on the wire and in the text layout.</summary>
    /// <param name="verb">The verb.</param>
    /// <returns>The name.</returns>
    public static string Of(StatusVerb verb) => verb switch
    {
        StatusVerb.Answer => "answer",
        StatusVerb.Complete => "complete",
        StatusVerb.Handback => "handback",
        StatusVerb.Takeover => "takeover",
        StatusVerb.Cancel => "cancel",
        StatusVerb.Approve => "approve",
        StatusVerb.Rewind => "rewind",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "no such status verb"),
    };
}
