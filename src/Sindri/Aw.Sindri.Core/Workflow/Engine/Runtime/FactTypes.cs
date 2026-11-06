namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The fact types a fact row serves (API spec §3.8); a type this build doesn't know is served as 'payload'.</summary>
public static class FactTypes
{
    /// <summary>The type of anything else.</summary>
    public const string Payload = "payload";

    private static readonly string[] _known = ["text", "file", "item", "link", "scope-map", "packet", "evidence", "decision", Payload, "findings"];

    /// <summary>The declared type as the view's enum; a name this build doesn't know is payload.</summary>
    /// <param name="declared">The recipe's type text.</param>
    /// <returns>The type.</returns>
    public static Views.FactType Parse(string? declared) => Of(declared) switch
    {
        "text" => Views.FactType.Text,
        "file" => Views.FactType.File,
        "item" => Views.FactType.Item,
        "link" => Views.FactType.Link,
        "scope-map" => Views.FactType.ScopeMap,
        "packet" => Views.FactType.Packet,
        "evidence" => Views.FactType.Evidence,
        "decision" => Views.FactType.Decision,
        "findings" => Views.FactType.Findings,
        _ => Views.FactType.Payload,
    };

    /// <summary>The served type of a declared one.</summary>
    /// <param name="declared">The recipe's 'type', or null.</param>
    /// <returns>The declared type when it is one of the ten, else 'payload'.</returns>
    public static string Of(string? declared) => declared is not null && Array.IndexOf(_known, declared) >= 0 ? declared : Payload;
}
