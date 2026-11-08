namespace Aw.Meta.Core.Routing;

/// <summary>Move one lane to another provider.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="FromAgent">Its agent now.</param>
/// <param name="FromModel">Its model now.</param>
/// <param name="ToAgent">The agent to move to.</param>
/// <param name="ToModel">The equivalent model there.</param>
/// <param name="Reason">Why.</param>
public sealed record RouteSuggestion(string Lane, string FromAgent, string FromModel, string ToAgent, string ToModel, string Reason)
{
    /// <summary>What identifies the suggestion across polls (the reason is left out: a limit's reset time changes).</summary>
    public string Key => $"{Lane}:{ToAgent}/{ToModel}";

    /// <summary>The line the CLI prints.</summary>
    /// <returns>switch lane from a/m to b/n because reason.</returns>
    public override string ToString() => $"switch {Lane} from {FromAgent}/{FromModel} to {ToAgent}/{ToModel} because {Reason}";
}
